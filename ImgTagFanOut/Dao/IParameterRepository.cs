using System;

namespace ImgTagFanOut.Dao;

public interface IParameterRepository
{
    string? Get(string name);
    void Update(string name, string? value);
    T? Get<T>(string name, Func<string?, T?> convert);
    void Update<T>(string name, T? value, Func<T?, string?> convert);
}