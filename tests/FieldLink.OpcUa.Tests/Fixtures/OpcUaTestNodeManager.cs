using System;
using System.Collections.Generic;
using System.Threading;
using Opc.Ua;
using Opc.Ua.Server;

namespace FieldLink.OpcUa.Tests.Fixtures;

internal sealed class OpcUaTestNodeManager : CustomNodeManager2
{
    public const string NamespaceUri = "urn:fieldlink:opcua:test";

    private int _writeExecutionCount;
    private int _slowReadDelayMs;
    private int _slowWriteDelayMs;
    private int _methodExecutionCount;
    private int _slowMethodDelayMs;

    private BaseDataVariableState<int>? _streamCounterVariable;
    private BaseDataVariableState<byte[]>? _streamLargeVariable;

    public OpcUaTestNodeManager(IServerInternal server, ApplicationConfiguration configuration)
        : base(server, configuration, NamespaceUri)
    {
        SystemContext.NodeIdFactory = this;
    }

    public int WriteExecutionCount => Volatile.Read(ref _writeExecutionCount);

    public int MethodExecutionCount => Volatile.Read(ref _methodExecutionCount);

    public void SetSlowReadDelay(TimeSpan delay)
    {
        Volatile.Write(ref _slowReadDelayMs, Math.Max(0, (int)delay.TotalMilliseconds));
    }

    public void SetSlowWriteDelay(TimeSpan delay)
    {
        Volatile.Write(ref _slowWriteDelayMs, Math.Max(0, (int)delay.TotalMilliseconds));
    }

    public void SetSlowMethodDelay(TimeSpan delay)
    {
        Volatile.Write(ref _slowMethodDelayMs, Math.Max(0, (int)delay.TotalMilliseconds));
    }

    public void ResetCounters()
    {
        Volatile.Write(ref _writeExecutionCount, 0);
        Volatile.Write(ref _methodExecutionCount, 0);
    }

    public void UpdateStreamCounter(int value, bool preserveBadOrUncertain = false)
    {
        if (_streamCounterVariable is null)
        {
            return;
        }

        lock (Lock)
        {
            _streamCounterVariable.Value = value;
            _streamCounterVariable.StatusCode = preserveBadOrUncertain
                ? StatusCodes.UncertainSensorNotAccurate
                : StatusCodes.Good;
            _streamCounterVariable.Timestamp = DateTime.UtcNow;
            _streamCounterVariable.ClearChangeMasks(SystemContext, includeChildren: false);
        }
    }

    public void UpdateStreamLargePayload(byte[] payload)
    {
        if (_streamLargeVariable is null)
        {
            return;
        }

        byte[] snapshot = (byte[])payload.Clone();
        lock (Lock)
        {
            _streamLargeVariable.Value = snapshot;
            _streamLargeVariable.StatusCode = StatusCodes.Good;
            _streamLargeVariable.Timestamp = DateTime.UtcNow;
            _streamLargeVariable.ClearChangeMasks(SystemContext, includeChildren: false);
        }
    }

    public override NodeId New(ISystemContext context, NodeState node)
    {
        if (node is BaseInstanceState instance &&
            instance.Parent is not null &&
            instance.Parent.NodeId.Identifier is string parentId)
        {
            return new NodeId($"{parentId}.{instance.BrowseName.Name}", NamespaceIndex);
        }

        return new NodeId(node.BrowseName.Name, NamespaceIndex);
    }

    public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
    {
        lock (Lock)
        {
            if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out IList<IReference>? references))
            {
                references = new List<IReference>();
                externalReferences[ObjectIds.ObjectsFolder] = references;
            }

            FolderState rootFolder = CreateFolder(null, "TestRoot");
            rootFolder.AddReference(ReferenceTypeIds.Organizes, isInverse: true, ObjectIds.ObjectsFolder);
            references.Add(new NodeStateReference(ReferenceTypeIds.Organizes, isInverse: false, rootFolder.NodeId));
            rootFolder.EventNotifier = EventNotifiers.None;

            FolderState scalarFolder = CreateFolder(rootFolder, "Scalar");
            CreateVariable(scalarFolder, "Boolean", DataTypeIds.Boolean, ValueRanks.Scalar, true);
            CreateVariable(scalarFolder, "Int32", DataTypeIds.Int32, ValueRanks.Scalar, 42);
            CreateVariable(scalarFolder, "Double", DataTypeIds.Double, ValueRanks.Scalar, 3.5);
            CreateVariable(scalarFolder, "String", DataTypeIds.String, ValueRanks.Scalar, "initial");

            FolderState arrayFolder = CreateFolder(rootFolder, "Array");
            CreateVariable(arrayFolder, "Int32", DataTypeIds.Int32, ValueRanks.OneDimension, new[] { 1, 2, 3 });
            CreateVariable(arrayFolder, "Bytes", DataTypeIds.Byte, ValueRanks.OneDimension, new byte[] { 10, 20, 30 });

            FolderState specialFolder = CreateFolder(rootFolder, "Special");
            CreateVariable(specialFolder, "ReadOnlyInt32", DataTypeIds.Int32, ValueRanks.Scalar, 99, AccessLevels.CurrentRead);
            BaseDataVariableState<int> uncertainVariable = CreateVariable(specialFolder, "UncertainInt32", DataTypeIds.Int32, ValueRanks.Scalar, 123, AccessLevels.CurrentRead);
            uncertainVariable.StatusCode = StatusCodes.UncertainSensorNotAccurate;
            BaseDataVariableState<int> badVariable = CreateVariable(specialFolder, "BadInt32", DataTypeIds.Int32, ValueRanks.Scalar, 0, AccessLevels.CurrentRead);
            badVariable.StatusCode = StatusCodes.BadWaitingForInitialData;

            BaseDataVariableState<int> slowReadVariable = CreateVariable(specialFolder, "SlowReadInt32", DataTypeIds.Int32, ValueRanks.Scalar, 777);
            slowReadVariable.OnSimpleReadValue = ReadSlowVariable;

            BaseDataVariableState<int> slowWriteVariable = CreateVariable(specialFolder, "SlowWriteInt32", DataTypeIds.Int32, ValueRanks.Scalar, 0);
            slowWriteVariable.OnSimpleWriteValue = WriteSlowVariable;

            _streamCounterVariable = CreateVariable(specialFolder, "StreamCounter", DataTypeIds.Int32, ValueRanks.Scalar, 0);
            _streamCounterVariable.MinimumSamplingInterval = 0;

            _streamLargeVariable = CreateVariable(specialFolder, "StreamLarge", DataTypeIds.Byte, ValueRanks.OneDimension, new byte[64]);
            _streamLargeVariable.MinimumSamplingInterval = 0;

            FolderState browseFolder = CreateFolder(rootFolder, "BrowseFolder");
            for (int i = 0; i < 25; i++)
            {
                CreateVariable(browseFolder, $"Item{i:D2}", DataTypeIds.Int32, ValueRanks.Scalar, i);
            }

            CreateMethods(rootFolder);
            AddPredefinedNode(SystemContext, rootFolder);
        }
    }

    private void CreateMethods(FolderState rootFolder)
    {
        FolderState methodsFolder = CreateFolder(rootFolder, "Methods");

        MethodState addMethod = new(methodsFolder)
        {
            SymbolicName = "Add",
            BrowseName = new QualifiedName("Add", NamespaceIndex),
            DisplayName = new LocalizedText("Add"),
            NodeId = new NodeId("TestRoot.Methods.Add", NamespaceIndex),
            Executable = true,
            UserExecutable = true
        };
        addMethod.InputArguments = CreateArgumentVariable(
            addMethod,
            "InputArguments",
            new Argument[]
            {
                new() { Name = "left", DataType = DataTypeIds.Int32, ValueRank = ValueRanks.Scalar },
                new() { Name = "right", DataType = DataTypeIds.Int32, ValueRank = ValueRanks.Scalar }
            });
        addMethod.OutputArguments = CreateArgumentVariable(
            addMethod,
            "OutputArguments",
            new Argument[]
            {
                new() { Name = "sum", DataType = DataTypeIds.Int32, ValueRank = ValueRanks.Scalar }
            });
        addMethod.OnCallMethod = OnAddMethodCalled;
        methodsFolder.AddChild(addMethod);

        MethodState slowMethod = new(methodsFolder)
        {
            SymbolicName = "SlowExecute",
            BrowseName = new QualifiedName("SlowExecute", NamespaceIndex),
            DisplayName = new LocalizedText("SlowExecute"),
            NodeId = new NodeId("TestRoot.Methods.SlowExecute", NamespaceIndex),
            Executable = true,
            UserExecutable = true
        };
        slowMethod.InputArguments = CreateArgumentVariable(
            slowMethod,
            "InputArguments",
            new Argument[]
            {
                new() { Name = "input", DataType = DataTypeIds.Int32, ValueRank = ValueRanks.Scalar }
            });
        slowMethod.OutputArguments = CreateArgumentVariable(
            slowMethod,
            "OutputArguments",
            new Argument[]
            {
                new() { Name = "output", DataType = DataTypeIds.Int32, ValueRank = ValueRanks.Scalar }
            });
        slowMethod.OnCallMethod = OnSlowMethodCalled;
        methodsFolder.AddChild(slowMethod);

        MethodState denyMethod = new(methodsFolder)
        {
            SymbolicName = "DenyAccess",
            BrowseName = new QualifiedName("DenyAccess", NamespaceIndex),
            DisplayName = new LocalizedText("DenyAccess"),
            NodeId = new NodeId("TestRoot.Methods.DenyAccess", NamespaceIndex),
            Executable = true,
            UserExecutable = true
        };
        denyMethod.OnCallMethod = static (_, _, _, _) => StatusCodes.BadUserAccessDenied;
        methodsFolder.AddChild(denyMethod);
    }

    private PropertyState<Argument[]> CreateArgumentVariable(
        MethodState method,
        string name,
        Argument[] arguments)
    {
        return new PropertyState<Argument[]>(method)
        {
            NodeId = new NodeId($"{method.NodeId.Identifier}.{name}", NamespaceIndex),
            BrowseName = name == "InputArguments" ? BrowseNames.InputArguments : BrowseNames.OutputArguments,
            DisplayName = name,
            TypeDefinitionId = VariableTypeIds.PropertyType,
            ReferenceTypeId = ReferenceTypeIds.HasProperty,
            DataType = DataTypeIds.Argument,
            ValueRank = ValueRanks.OneDimension,
            Value = arguments
        };
    }

    private FolderState CreateFolder(NodeState? parent, string name)
    {
        FolderState folder = new(parent)
        {
            SymbolicName = name,
            BrowseName = new QualifiedName(name, NamespaceIndex),
            DisplayName = new LocalizedText(name)
        };
        folder.NodeId = New(SystemContext, folder);
        parent?.AddChild(folder);
        return folder;
    }

    private BaseDataVariableState<T> CreateVariable<T>(
        NodeState parent,
        string name,
        NodeId dataType,
        int valueRank,
        T initialValue,
        byte accessLevel = AccessLevels.CurrentReadOrWrite)
    {
        BaseDataVariableState<T> variable = new(parent)
        {
            SymbolicName = name,
            BrowseName = new QualifiedName(name, NamespaceIndex),
            DisplayName = new LocalizedText(name),
            DataType = dataType,
            ValueRank = valueRank,
            AccessLevel = accessLevel,
            UserAccessLevel = accessLevel,
            Value = initialValue,
            StatusCode = StatusCodes.Good,
            Timestamp = DateTime.UtcNow,
            Historizing = false
        };
        variable.NodeId = New(SystemContext, variable);
        parent.AddChild(variable);
        return variable;
    }

    private ServiceResult ReadSlowVariable(
        ISystemContext context,
        NodeState node,
        ref object? value)
    {
        int delayMs = Volatile.Read(ref _slowReadDelayMs);
        if (delayMs > 0)
        {
            Thread.Sleep(delayMs);
        }

        return ServiceResult.Good;
    }

    private ServiceResult WriteSlowVariable(
        ISystemContext context,
        NodeState node,
        ref object? value)
    {
        Interlocked.Increment(ref _writeExecutionCount);
        int delayMs = Volatile.Read(ref _slowWriteDelayMs);
        if (delayMs > 0)
        {
            Thread.Sleep(delayMs);
        }

        return ServiceResult.Good;
    }

    private ServiceResult OnAddMethodCalled(
        ISystemContext context,
        MethodState method,
        IList<object> inputArguments,
        IList<object> outputArguments)
    {
        if (inputArguments.Count != 2 ||
            inputArguments[0] is not int left ||
            inputArguments[1] is not int right)
        {
            return StatusCodes.BadInvalidArgument;
        }

        Interlocked.Increment(ref _methodExecutionCount);
        outputArguments[0] = left + right;
        return ServiceResult.Good;
    }

    private ServiceResult OnSlowMethodCalled(
        ISystemContext context,
        MethodState method,
        IList<object> inputArguments,
        IList<object> outputArguments)
    {
        if (inputArguments.Count != 1 || inputArguments[0] is not int input)
        {
            return StatusCodes.BadInvalidArgument;
        }

        Interlocked.Increment(ref _methodExecutionCount);
        int delayMs = Volatile.Read(ref _slowMethodDelayMs);
        if (delayMs > 0)
        {
            Thread.Sleep(delayMs);
        }

        outputArguments[0] = input * 2;
        return ServiceResult.Good;
    }
}
