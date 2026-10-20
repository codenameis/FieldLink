namespace FieldLink.Plc.Protocols.Delta;

/// <summary>Delta PLC 시리즈 구분입니다. (DVP-0139720-10 및 AS-0249720 사양)</summary>
public enum DeltaSeries
{
    /// <summary>DVP 시리즈 (ES2, EX2, EC5, SS2, SA2, SX2, SE, EH3 등)</summary>
    Dvp,

    /// <summary>AS 시리즈 (AS300, AS200 등)</summary>
    AS,
}
