using System;
using System.Reflection;
using FieldLink.Robot.Protocols.Abb.Rws;
using FieldLink.Robot.Protocols.Efort.ER7BC10;
using FieldLink.Robot.Protocols.Estun.Modbus;
using FieldLink.Robot.Protocols.Fanuc.RobotInterface;
using FieldLink.Robot.Protocols.Hyundai.OnLTrack;
using FieldLink.Robot.Protocols.Kuka.SocketAscii;
using FieldLink.Robot.Protocols.Kuka.VarProxy;
using FieldLink.Robot.Protocols.Yamaha.Rcx;
using FieldLink.Robot.Protocols.Yaskawa.HighSpeedEthernet;
using FieldLink.Robot.Protocols.Yaskawa.YrcEthernet;
using FieldLink.Robot.Tests.Fakes;
using FieldLink.Transport;
using FieldLink.Transport.Http;
using FieldLink.Transport.Tcp;
using FieldLink.Transport.Udp;
using Xunit;

namespace FieldLink.Robot.Tests;

public sealed class RobotClientFactoryTests
{
    [Fact]
    public void CreateClients_WithHostAndPort_ConstructAll11ProtocolVariants()
    {
        using AbbRwsClient abb = RobotClientFactory.CreateAbbRws("192.168.125.1");
        using AbbRwsClient abbHttps = RobotClientFactory.CreateAbbRws(new AbbRwsOptions("192.168.125.1", 443, useHttps: true));
        using EfortClient efortV2 = RobotClientFactory.CreateEfortER7BC10V2("192.168.1.10");
        using EfortClient efortV1 = RobotClientFactory.CreateEfortER7BC10V1("192.168.1.10");
        using EstunClient estun = RobotClientFactory.CreateEstunModbus("192.168.1.11");
        using FanucClient fanuc = RobotClientFactory.CreateFanucRobotInterface("192.168.1.12");
        using HyundaiOnLTrackClient hyundai = RobotClientFactory.CreateHyundaiOnLTrack(0);
        using KukaVarProxyClient kukaVarProxy = RobotClientFactory.CreateKukaVarProxy("192.168.1.13");
        using KukaSocketAsciiClient kukaAscii = RobotClientFactory.CreateKukaSocketAscii("192.168.1.13");
        using YamahaRcxClient yamaha = RobotClientFactory.CreateYamahaRcx("192.168.1.14");
        using YrcEthernetClient yrcTcp = RobotClientFactory.CreateYaskawaYrcEthernet("192.168.1.15");
        using YercUdpClient yercUdp = RobotClientFactory.CreateYaskawaHighSpeedEthernet("192.168.1.15");

        Assert.NotNull(abb);
        Assert.NotNull(abbHttps);
        Assert.NotNull(efortV2);
        Assert.NotNull(efortV1);
        Assert.NotNull(estun);
        Assert.NotNull(fanuc);
        Assert.NotNull(hyundai);
        Assert.NotNull(kukaVarProxy);
        Assert.NotNull(kukaAscii);
        Assert.NotNull(yamaha);
        Assert.NotNull(yrcTcp);
        Assert.NotNull(yercUdp);
    }

    [Fact]
    public void CreateClients_WithInjectedTransports_AndInvalidInputs_ValidateProperly()
    {
        using var http = new FakeHttpTransport();
        using var transport = new FakeTransport();
        using var udpListener = new FakeUdpListenerTransport();

        using AbbRwsClient abb = RobotClientFactory.CreateAbbRws(http);
        using EfortClient efort = RobotClientFactory.CreateEfortER7BC10(transport);
        using EstunClient estun = RobotClientFactory.CreateEstunModbus(transport);
        using FanucClient fanuc = RobotClientFactory.CreateFanucRobotInterface(transport);
        using HyundaiOnLTrackClient hyundai = RobotClientFactory.CreateHyundaiOnLTrack(udpListener);
        using KukaVarProxyClient kukaVarProxy = RobotClientFactory.CreateKukaVarProxy(transport);
        using KukaSocketAsciiClient kukaAscii = RobotClientFactory.CreateKukaSocketAscii(transport);
        using YamahaRcxClient yamaha = RobotClientFactory.CreateYamahaRcx(transport);
        using YrcEthernetClient yrcTcp = RobotClientFactory.CreateYaskawaYrcEthernet(transport);
        using YercUdpClient yercUdp = RobotClientFactory.CreateYaskawaHighSpeedEthernet(transport);

        Assert.False(abb.IsOpen);
        Assert.False(efort.IsOpen);
        Assert.False(estun.IsOpen);
        Assert.False(fanuc.IsOpen);
        Assert.False(hyundai.IsOpen);
        Assert.False(kukaVarProxy.IsOpen);
        Assert.False(kukaAscii.IsOpen);
        Assert.False(yamaha.IsOpen);
        Assert.False(yrcTcp.IsOpen);
        Assert.False(yercUdp.IsOpen);

        Assert.Throws<ArgumentNullException>(() => RobotClientFactory.CreateAbbRws((IHttpTransport)null!));
        Assert.Throws<ArgumentNullException>(() => RobotClientFactory.CreateEstunModbus((ITransport)null!));
        Assert.Throws<ArgumentNullException>(() => RobotClientFactory.CreateHyundaiOnLTrack((IUdpListenerTransport)null!));
        Assert.Throws<ArgumentException>(() => RobotClientFactory.CreateFanucRobotInterface(string.Empty));
    }

    [Fact]
    public void CreateClients_ForwardOptionTimeoutsToUnderlyingTransports()
    {
        TimeSpan customTimeout = TimeSpan.FromSeconds(17);

        using YercUdpClient yercCustom = RobotClientFactory.CreateYaskawaHighSpeedEthernet(
            "192.168.1.15",
            options: new YercUdpOptions { Timeout = customTimeout });
        Assert.Equal(customTimeout, GetUnderlyingUdpOptions(yercCustom).ReceiveTimeout);

        using YamahaRcxClient yamahaDefault = RobotClientFactory.CreateYamahaRcx("192.168.1.14");
        Assert.Equal(TimeSpan.FromSeconds(30), GetUnderlyingTcpOptions(yamahaDefault).ConnectTimeout);

        using YamahaRcxClient yamahaCustom = RobotClientFactory.CreateYamahaRcx(
            "192.168.1.14",
            options: new YamahaRcxOptions { Timeout = customTimeout });
        Assert.Equal(customTimeout, GetUnderlyingTcpOptions(yamahaCustom).ConnectTimeout);

        using EstunClient estunCustom = RobotClientFactory.CreateEstunModbus(
            "192.168.1.11",
            options: new EstunOptions { Timeout = customTimeout });
        Assert.Equal(customTimeout, GetUnderlyingTcpOptions(estunCustom).ConnectTimeout);

        using EfortClient efortCustom = RobotClientFactory.CreateEfortER7BC10V2(
            "192.168.1.10",
            options: new EfortOptions { Timeout = customTimeout });
        Assert.Equal(customTimeout, GetUnderlyingTcpOptions(efortCustom).ConnectTimeout);

        using FanucClient fanucCustom = RobotClientFactory.CreateFanucRobotInterface(
            "192.168.1.12",
            options: new FanucOptions { Timeout = customTimeout });
        Assert.Equal(customTimeout, GetUnderlyingTcpOptions(fanucCustom).ConnectTimeout);

        using KukaVarProxyClient kukaVarCustom = RobotClientFactory.CreateKukaVarProxy(
            "192.168.1.13",
            options: new KukaVarProxyOptions { Timeout = customTimeout });
        Assert.Equal(customTimeout, GetUnderlyingTcpOptions(kukaVarCustom).ConnectTimeout);

        using KukaSocketAsciiClient kukaAsciiCustom = RobotClientFactory.CreateKukaSocketAscii(
            "192.168.1.13",
            options: new KukaSocketAsciiOptions { Timeout = customTimeout });
        Assert.Equal(customTimeout, GetUnderlyingTcpOptions(kukaAsciiCustom).ConnectTimeout);

        using YrcEthernetClient yrcCustom = RobotClientFactory.CreateYaskawaYrcEthernet(
            "192.168.1.15",
            options: new YrcEthernetOptions { Timeout = customTimeout });
        Assert.Equal(customTimeout, GetUnderlyingTcpOptions(yrcCustom).ConnectTimeout);
    }

    private static TcpTransportOptions GetUnderlyingTcpOptions(object client)
    {
        FieldInfo? transportField = client.GetType().GetField("_transport", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(transportField);
        object? transport = transportField.GetValue(client);
        Assert.IsType<TcpTransport>(transport);
        FieldInfo? optionsField = typeof(TcpTransport).GetField("_options", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(optionsField);
        return Assert.IsType<TcpTransportOptions>(optionsField.GetValue(transport));
    }

    private static UdpTransportOptions GetUnderlyingUdpOptions(object client)
    {
        FieldInfo? transportField = client.GetType().GetField("_transport", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(transportField);
        object? transport = transportField.GetValue(client);
        Assert.IsType<UdpTransport>(transport);
        FieldInfo? optionsField = typeof(UdpTransport).GetField("_options", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(optionsField);
        return Assert.IsType<UdpTransportOptions>(optionsField.GetValue(transport));
    }
}
