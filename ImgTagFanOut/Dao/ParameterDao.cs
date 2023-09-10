using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace ImgTagFanOut.Dao;

[DebuggerDisplay("{Name}-{Value}-{ParameterId}")]
public class ParameterDao
{
    [Key] public int ParameterId { get; set; }

    public string Name { get; set; } = null!;
    public string? Value { get; set; }
}