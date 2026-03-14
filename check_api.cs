using System;
using System.Reflection;
using SharpCompress.Writers;
using SharpCompress.Archives;

public class Checker {
    public static void Main() {
        Console.WriteLine("WriterFactory methods:");
        foreach(var m in typeof(WriterFactory).GetMethods()) Console.WriteLine(" - " + m.Name);
        
        Console.WriteLine("\nArchiveFactory methods:");
        foreach(var m in typeof(ArchiveFactory).GetMethods()) Console.WriteLine(" - " + m.Name);
    }
}
