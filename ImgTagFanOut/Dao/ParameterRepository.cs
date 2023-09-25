using System;
using System.Linq;

namespace ImgTagFanOut.Dao;

internal class ParameterRepository : IParameterRepository
{
    private readonly IImgTagFanOutDbContext _dbContext;

    public ParameterRepository(IImgTagFanOutDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string? Get(string name)
    {
        ParameterDao? parameter = FindParameterByName(name);

        return parameter?.Value;
    }

    private ParameterDao? FindParameterByName(string name)
    {
        return _dbContext.Parameters.FirstOrDefault(x => x.Name == name);
    }

    public void Update(string name, string? value)
    {
        ParameterDao? parameter = FindParameterByName(name);

        if (parameter == null)
        {
            parameter = new() { Name = name, Value = value };
            _dbContext.Parameters.Add(parameter);
        }

        parameter.Value = value;
    }

    public T? Get<T>(string name, Func<string?, T?> convert)
    {
        string? rawValue = Get(name);

        return convert(rawValue);
    }

    public void Update<T>(string name, T? value, Func<T?, string?> convert)
    {
        string? rawValue = convert(value);

        Update(name, rawValue);
    }
}