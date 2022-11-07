using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TwitchVor.Finisher;

public class ProcessingHandler
{
    readonly TaskCompletionSource processTCS = new();

    public readonly ProcessingVideo[] videos;

    public Task ProcessTask => processTCS.Task;

    public ProcessingHandler(ProcessingVideo[] videos)
    {
        this.videos = videos;
    }

    public void SetResult()
    {
        processTCS.SetResult();
    }
}
