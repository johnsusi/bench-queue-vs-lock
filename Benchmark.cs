using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Exporters.Csv;

namespace Benchmark
{

  [MemoryDiagnoser]
  [CsvMeasurementsExporter]
  [RPlotExporter]
  public class Benchmark
  {
    [Params(1, 10, 100)]
    public int Workers;
    private byte[] Data = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

    private static UInt32 hash(byte[] data)
    {
      UInt32 hash = 5381;
      foreach (byte c in data)
        hash = hash * 33 + c;
      return hash;
    }

    [Benchmark(Baseline = true)]
    public UInt32 Baseline()
    {
      using var stream = new MemoryStream();
      for (int i = 0; i < Workers; ++i)
        stream.Write(Data);
      return hash(stream.ToArray());
    }

    [Benchmark]
    public async Task<UInt32> Lock()
    {
      using var stream = new MemoryStream();
      object l = new object();

      var workers =
        Enumerable
          .Range(1, Workers)
          .Select(id => Task.Run(worker));

      await Task.WhenAll(workers);

      void worker()
      {
        lock (l)
        {
          stream.Write(Data);
        }
      }

      return hash(stream.ToArray());
    }

    [Benchmark]
    public async Task<UInt32> Queue()
    {
      using var stream = new MemoryStream();
      var chan = Channel.CreateUnbounded<Action>();

      var task = Task.Run(async () =>
      {
        await foreach (var task in chan.Reader.ReadAllAsync())
          task();
      });

      for (int i = 1; i <= Workers; ++i)
        chan.Writer.TryWrite(worker);

      chan.Writer.TryComplete();

      await task;
      return hash(stream.ToArray());

      void worker()
      {
        stream.Write(Data);
      }

    }

    [Benchmark]
    public async Task<UInt32> LockSemaphoreSlim()
    {

      using var stream = new MemoryStream();
      var semaphore = new SemaphoreSlim(1, 1);

      var workers =
        Enumerable
          .Range(1, Workers)
          .Select(id => Task.Run(worker));

      await Task.WhenAll(workers);

      void worker()
      {
        try
        {
          semaphore.Wait();
          stream.Write(Data);
        }
        finally
        {
          semaphore.Release();
        }
      }

      return hash(stream.ToArray());
    }

  }
}