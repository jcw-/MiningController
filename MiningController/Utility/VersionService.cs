using Octokit;
using System;
using System.Linq;
using System.Reflection;

namespace MiningController
{
    public class VersionService : IVersionService
    {
        private Lazy<string> currentVersion { get; set; }
        private IReleasesClient releasesClient;
        private string owner;
        private string repo;
        private readonly TimeSpan MaxApiWaitTime = TimeSpan.FromMinutes(1);
        private readonly object padlock = new object();

        public VersionService(IReleasesClient client, string owner, string repo)
        {
            this.currentVersion = new Lazy<string>(new Func<string>(() => RetrieveInformationalVersion(Assembly.GetExecutingAssembly())));
            this.releasesClient = client;
            this.owner = owner;
            this.repo = repo;
        }

        public string CurrentVersion
        {
            get
            {
                return this.currentVersion.Value;
            }
        }

        public void IsUpdateAvailableAsync(Action<bool> callback)
        {
            var worker = new System.ComponentModel.BackgroundWorker();
            worker.DoWork += (s, e) =>
            {
                lock (this.padlock)
                {
                    callback(IsUpdateAvailable());
                }
            };

            worker.RunWorkerAsync();
        }

        public bool IsUpdateAvailable()
        {
            bool updateAvailable = false;

            try
            {
                var task = releasesClient.GetAll(this.owner, this.repo);
                if (task.Wait(MaxApiWaitTime))
                {
                    updateAvailable = task.Result.Where(r => !r.Draft && !r.Prerelease).Any(r => this.IsNewer(r.TagName));
                }
            }
            catch (Exception)
            {
                return true; // if something breaks in this code, users should check the site as there will hopefully be a fix available in the form of a new version
            }

            return updateAvailable;
        }

        private bool IsNewer(string version)
        {
            var newestVersion = version.ToLowerInvariant().Replace("v", string.Empty);
            var currentVersion = this.currentVersion.Value.ToLowerInvariant().Replace("v", string.Empty);

            // remove any appended details, such as -beta, or a commit id
            newestVersion = StripPostFix(newestVersion);
            currentVersion = StripPostFix(currentVersion);

            return Version.Parse(newestVersion) > Version.Parse(currentVersion);
        }

        private static string StripPostFix(string version)
        {
            version = StripPostFix(version, "-");
            version = StripPostFix(version, "+");

            return version;
        }

        private static string StripPostFix(string version, string start)
        {
            var index = version.IndexOf(start);
            if (index > 0)
            {
                version = version.Substring(0, index);
            }

            return version;
        }

        public static string RetrieveInformationalVersion(Assembly assembly)
        {
            var attribute = (AssemblyInformationalVersionAttribute)assembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false).FirstOrDefault();
            if (attribute != null)
            {
                return attribute.InformationalVersion;
            }

            return string.Empty; // this should never happen, but if it does, it causes the new version flag to go true, which is a good fail-safe
        }
    }
}
