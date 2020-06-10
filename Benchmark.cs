using System;
using System.IO;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Benchmark
{

  [MemoryDiagnoser]
  [RPlotExporter]
  public class Benchmark
  {
    [Params(1, 10, 100)]
    public int Workers;
    private byte[] Data = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9};

    [Benchmark]
    public async Task Lock()
    {

      using var stream = new MemoryStream(Workers * Data.Length);
      object l = new object();

      var workers =
        Enumerable
          .Range(1, Workers)
          .Select(id => Task.Run(() => worker(id, stream)));


      await Task.WhenAll(workers);

      void worker(int id, Stream stream)
      {
        lock(l)
        {
          stream.Write(Data);
        }
      }
    }

    [Benchmark]
    public async Task Queue()
    {

      using var stream = new MemoryStream(Workers * Data.Length);
      var chan = Channel.CreateUnbounded<Action>(new UnboundedChannelOptions
      {
        AllowSynchronousContinuations = true,
        SingleReader = true,
        SingleWriter = true
      });

      var workers =
        Enumerable
          .Range(1, Workers)
          .Select(id => chan.Writer.TryWrite(() => worker(id, stream)));

      chan.Writer.TryComplete();


      await foreach (var task in chan.Reader.ReadAllAsync())
        task();


      void worker(int id, Stream stream)
      {
        stream.Write(Data);
      }
    }

  }
}