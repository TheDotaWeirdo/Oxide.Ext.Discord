﻿namespace Oxide.Ext.Discord.REST
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using Oxide.Core;

    public class Bucket : List<Request>
    {
        public RequestMethod Method { get; }

        public string Route { get; }

        public int Limit { get; set; }

        public int Remaining { get; set; }

        public int Reset { get; set; }

        public bool Initialized { get; private set; } = false;

        public bool Disposed { get; set; } = false;

        private Thread thread;

        public Bucket(RequestMethod method, string route)
        {
            this.Method = method;
            this.Route = route;

            thread = new Thread(() => RunThread());
            thread.Start();
        }

        public void Close()
        {
            thread?.Abort();
            thread = null;
        }

        public void Queue(Request request)
        {
            this.Add(request);

            if (!this.Initialized)
            {
                this.Initialized = true;
            }
        }

        private void RunThread()
        {
            // 'Initialized' basically allows us to start the while
            // loop from the constructor even when this.Count = 0
            // (eg after the bucket is created, before requests are added)
            while (!Initialized || (this.Count > 0))
            {
                if (Disposed) break;

                if (!Initialized) continue;

                FireRequests();
            }

            this.Disposed = true;
        }

        private void FireRequests()
        {
            this.CleanRequests();

            if (GlobalRateLimit.Hit)
            {
                return;
            }

            if (Remaining == 0 && Reset >= TimeSinceEpoch())
            {
                return;
            }

            if (this.Any(x => x.InProgress))
            {
                return;
            }

            var nextItem = this.First();
            nextItem.Fire(this);
        }

        private void CleanRequests()
        {
            List<Request> requests;
            lock (this)
            {
                requests = new List<Request>(this).Where(x => x.HasTimedOut()).ToList();
            }

            foreach (var req in requests)
            {
                Interface.Oxide.LogWarning($"[Discord Ext] Closing request (timed out): {req.Route + req.Endpoint} [{req.Method}]");
                req.Close();
            }
        }

        public double TimeSinceEpoch() => (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
    }
}
