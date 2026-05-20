using System;

[Flags]
public enum WaveType
{
    Standard = 0,
    Horde = 1 << 0,
    Elite = 1 << 1,
    Siege = 1 << 2,
}