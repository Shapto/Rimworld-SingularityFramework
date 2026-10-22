using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SingularityFramework.Kitbash
{
    /// <summary>
    /// Runs kitbash builds off the main thread, so building a mask or a replica doesn't stall the game. The engine itself
    /// only does maths on arrays, so it's safe to run on any thread; reading textures before and making textures after must
    /// happen on the main thread, so the result is handed back there.
    /// </summary>
    public static class KitbashBuilds
    {
        private static readonly Queue<Action> finishedBuilds = new Queue<Action>();

        /// <summary>
        /// Runs "build" on a background thread, then "whenDone" with its result on the main thread (on a later frame).
        /// </summary>
        public static void Start<TResult>(Func<TResult> build, Action<TResult> whenDone, string description)
        {
            Task.Run(() =>
            {
                try
                {
                    TResult result = build();
                    lock (finishedBuilds) finishedBuilds.Enqueue(() => whenDone(result));
                }
                catch (Exception exception)
                {
                    lock (finishedBuilds) finishedBuilds.Enqueue(() => Log.Error($"[Singularity Framework] Kitbash build failed ({description}): {exception}"));
                }
            });
        }

        /// <summary>
        /// Hands every finished build back to the game. Called every frame on the main thread.
        /// </summary>
        public static void DeliverFinished()
        {
            while (true)
            {
                Action delivery;
                lock (finishedBuilds)
                {
                    if (finishedBuilds.Count == 0) return;
                    delivery = finishedBuilds.Dequeue();
                }
                try
                {
                    delivery();
                }
                catch (Exception exception)
                {
                    Log.Error($"[Singularity Framework] Delivering a kitbash build failed: {exception}");
                }
            }
        }
    }

    /// <summary>
    /// Delivers finished kitbash builds every frame while a game is running.
    /// </summary>
    public class GameComponent_KitbashBuilds : GameComponent
    {
        public GameComponent_KitbashBuilds(Game game)
        {
        }

        public override void GameComponentUpdate()
        {
            KitbashBuilds.DeliverFinished();
            KitbashGrowth.Update();
        }
    }
}
