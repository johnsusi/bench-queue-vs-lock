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
      byte[] data = Enumerable.Repeat((byte)0x00, Workers * Data.Length).ToArray();
      using var stream = new MemoryStream(data, true);
      for (int i = 0; i < Workers; ++i)
        stream.Write(Data);
      return hash(data);
    }

    [Benchmark]
    public async Task<UInt32> Lock()
    {

      byte[] data = Enumerable.Repeat((byte)0x00, Workers * Data.Length).ToArray();
      using var stream = new MemoryStream(data, true);
      object l = new object();

      var workers =
        Enumerable
          .Range(1, Workers)
          .Select(id => Task.Run(() => worker(id, stream)));


      await Task.WhenAll(workers);

      void worker(int id, Stream stream)
      {
        lock (l)
        {
          stream.Write(Data);
        }
      }

      return hash(data);
    }

    [Benchmark]
    public async Task<UInt32> Queue()
    {

      byte[] data = Enumerable.Repeat((byte)0x00, Workers * Data.Length).ToArray();
      using var stream = new MemoryStream(data, true);
      var chan = Channel.CreateUnbounded<Action>(new UnboundedChannelOptions
      {
        AllowSynchronousContinuations = true,
        SingleReader = true,
        SingleWriter = true
      });

      // IEnumerable<Action> workers =
      //   Enumerable
      //     .Range(1, Workers)
      //     .Select(id => () => worker(id, stream) );

      for (int i = 1; i <= Workers; ++i)
        chan.Writer.TryWrite(() => worker(i, stream));

      chan.Writer.TryComplete();


      await foreach (var task in chan.Reader.ReadAllAsync())
        task();


      void worker(int id, Stream stream)
      {
        stream.Write(Data);
      }


      return hash(data);

    }

    [Benchmark]
    public async Task<UInt32> LockSemaphoreSlim()
    {

      byte[] data = Enumerable.Repeat((byte)0x00, Workers * Data.Length).ToArray();
      using var stream = new MemoryStream(data, true);
      var semaphore = new SemaphoreSlim(1, 1);



      var workers =
        Enumerable
          .Range(1, Workers)
          .Select(id => Task.Run(() => worker(id, stream)));

      await Task.WhenAll(workers);

      void worker(int id, Stream stream)
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

      return hash(data);
    }

  }
}