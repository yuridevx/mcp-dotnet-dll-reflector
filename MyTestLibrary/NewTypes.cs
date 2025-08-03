using System.Runtime.InteropServices;

namespace MyTestLibrary;

[StructLayout(LayoutKind.Explicit, Pack = 1, Size = 8)]
public struct MyExplicitStruct
{
    [FieldOffset(0)]
    public long AllBits;

    [FieldOffset(0)]
    public int Int1;

    [FieldOffset(4)]
    public int Int2;
}

public interface IMyInterface
{
    string GetMessage();
}

public enum MyEnum
{
    ValueA = 0,
    ValueB = 10,
    ValueC = 11
}

public struct MyStruct
{
    public int Number { get; set; }
    public string Text { get; set; }

    public string GetInfo()
    {
        return $"Number: {Number}, Text: {Text}";
    }
}

public class MyGenericClass<T>
{
    public T Value { get; set; }

    public string GetValueType()
    {
        return typeof(T).Name;
    }
}