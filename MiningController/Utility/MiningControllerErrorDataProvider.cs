using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace MiningController
{
    public class MiningControllerErrorDataProvider : BugFreak.Components.IErrorDataProvider
    {
        private static readonly string[] excludeSettings = new string[] { "LaunchCommand", "ImportantProcessNames", "AutomaticErrorReporting", "SnoozeDurations" };
        private static readonly string CultureName = CultureInfo.CurrentUICulture.Name;        
        private static readonly string AssemblyInformationalVersion = VersionService.RetrieveInformationalVersion(Assembly.GetEntryAssembly());
        private static readonly string AssemblyVersion = Assembly.GetEntryAssembly().GetName().Version.ToString();
        private static readonly PropertyInfo[] SettingsProperties = MiningController.Properties.Settings.Default.GetType().GetProperties();
        private Lazy<string> AnonymousId;

        public MiningControllerErrorDataProvider()
        {
            this.AnonymousId = new Lazy<string>(InitializeAnonymousId);
        }
        
        public List<KeyValuePair<string, string>> GetData()
        {
            var data = new List<KeyValuePair<string, string>>
			{
#if DEBUG
                new KeyValuePair<string, string>("DEBUG", "true"),
#endif
                new KeyValuePair<string, string>("ReportedTimestampUtc", DateTime.UtcNow.ToString("r")),
                new KeyValuePair<string, string>("AssemblyInformationalVersion", AssemblyInformationalVersion),
                new KeyValuePair<string, string>("AssemblyVersion", AssemblyVersion),                
                new KeyValuePair<string, string>("Culture", CultureName),
                new KeyValuePair<string, string>("AnonymousId", this.AnonymousId.Value)
			};

            var settings = MiningController.Properties.Settings.Default;
            foreach (var prop in SettingsProperties)
            {
                if (excludeSettings.Contains(prop.Name))
                {
                    continue;
                }

                if (prop.GetCustomAttributes().Any(attr => attr is ApplicationScopedSettingAttribute || attr is UserScopedSettingAttribute))
                {
                    data.Add(new KeyValuePair<string, string>("Settings::" + prop.Name, prop.GetValue(settings).ToString()));
                }
            }

            return data;
        }

        private string InitializeAnonymousId()
        {
            string plaintext = string.Empty;

            // generate some plaintext based on the computer's hardware and then hash it,
            // this allows multiple errors from the same system to be grouped together, 
            // without actually providing any identifiable details
            try
            {
                var mc = new ManagementClass("Win32_Processor");
                var moc = mc.GetInstances();
                ManagementObject mo = null;
                foreach (ManagementObject o in moc)
                {
                    // get first instance
                    mo = o;
                    break;
                }

                if (mo == null)
                {
                    return string.Empty;
                }

                plaintext = RetrieveProperty(mo, new string[] { "UniqueId", "ProcessorId" });
                if (string.IsNullOrEmpty(plaintext))
                {                    
                    // backup way (for older processors) to get some hardware plaintext to hash
                    plaintext = RetrieveProperty(mo, new string[] { "Name", "Manufacturer" }) + RetrieveProperty(mo, new string[] { "MaxClockSpeed" });
                }
            }
            catch (Exception)
            {
                return string.Empty;
            }

            // hash the text to make it fully anonymous
            // Note: MD5 is fast and even though it is NOT a good choice for hashing passwords, it is appropriate for this usage
            using (MD5 md5Hash = MD5.Create())
            {
                return GetMd5Hash(md5Hash, plaintext);
            }
        }

        // from: http://msdn.microsoft.com/en-us/library/s02tk69a(v=vs.110).aspx
        private static string GetMd5Hash(MD5 md5Hash, string input)
        {
            // Convert the input string to a byte array and compute the hash. 
            byte[] data = md5Hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));

            // Create a new Stringbuilder to collect the bytes and create a string.
            var sb = new StringBuilder();

            // Loop through each byte of the hashed data and format each one as a hexadecimal string. 
            for (int i = 0; i < data.Length; i++)
            {
                sb.Append(data[i].ToString("x2"));
            }

            // Return the hexadecimal string. 
            return sb.ToString();
        }

        private static string RetrieveProperty(ManagementObject mo, IEnumerable<string> properties)
        {
            string result = string.Empty;

            foreach (var prop in properties)
            {
                try
                {
                    var o = mo[prop];
                    if (o != null)
                    {
                        var s = o.ToString();
                        if (!string.IsNullOrEmpty(s))
                        {
                            return s;
                        }
                    }
                }
                catch (Exception)
                {
                    continue;
                }
            }

            return string.Empty;
        }
    }
}
