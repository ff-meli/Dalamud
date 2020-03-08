using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Lumina.Data;
using Lumina.Excel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using LuminaOptions = Lumina.LuminaOptions;
using ParsedFilePath = Lumina.ParsedFilePath;

namespace Dalamud.Data
{
    /// <summary>
    /// This class provides data for Dalamud-internal features, but can also be used by plugins if needed.
    /// </summary>
    public class DataManager {
        private const string DataBaseUrl = "https://goaaats.github.io/ffxiv/tools/launcher/addons/Hooks/Data/";

        public ReadOnlyDictionary<string, ushort> ServerOpCodes { get; private set; }

        /// <summary>
        /// An <see cref="ExcelModule"/> object which gives access to any of the game's sheet data.
        /// </summary>
        public ExcelModule Excel => this.gameData?.Excel;

        /// <summary>
        /// Indicates whether Game Data is ready to be read.
        /// </summary>
        public bool IsDataReady { get; private set; }

        /// <summary>
        /// A <see cref="Lumina"/> object which gives access to any excel/game data.
        /// </summary>
        private Lumina.Lumina gameData;

        private ClientLanguage language;

        public DataManager(ClientLanguage language)
        {
            // Set up default values so plugins do not null-reference when data is being loaded.
            this.ServerOpCodes = new ReadOnlyDictionary<string, ushort>(new Dictionary<string, ushort>());

            this.language = language;
        }

        public async Task Initialize()
        {
            try
            {
                Log.Verbose("Starting data download...");

                using var client = new HttpClient()
                {
                    BaseAddress = new Uri(DataBaseUrl)
                };

                var opCodeDict =
                    JsonConvert.DeserializeObject<Dictionary<string, ushort>>(
                        await client.GetStringAsync(DataBaseUrl + "serveropcode.json"));
                this.ServerOpCodes = new ReadOnlyDictionary<string, ushort>(opCodeDict);

                Log.Verbose("Loaded {0} ServerOpCodes.", opCodeDict.Count);


                var luminaOptions = new LuminaOptions
                {
                    CacheFileResources = true
                };

                luminaOptions.DefaultExcelLanguage = this.language switch {
                    ClientLanguage.Japanese => Language.Japanese,
                    ClientLanguage.English => Language.English,
                    ClientLanguage.German => Language.German,
                    ClientLanguage.French => Language.French,
                    _ => throw new ArgumentOutOfRangeException(nameof(this.language),
                                                               "Unknown Language: " + this.language)
                };

                Log.Debug("Creating Lumina");

                gameData = new Lumina.Lumina(Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "sqpack"), luminaOptions);

                Log.Information("Lumina is ready: {0}", gameData.DataPath);

                IsDataReady = true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not download data.");
            }
        }

        #region Lumina Wrappers

        /// <summary>
        /// Get an <see cref="ExcelSheet{T}"/> with the given Excel sheet row type.
        /// </summary>
        /// <typeparam name="T">The excel sheet type to get.</typeparam>
        /// <returns>The <see cref="ExcelSheet{T}"/>, giving access to game rows.</returns>
        public ExcelSheet<T> GetExcelSheet<T>() where T : IExcelRow
        {
            return this.Excel.GetSheet<T>();
        }

        /// <summary>
        /// Get a <see cref="FileResource"/> with the given path.
        /// </summary>
        /// <param name="path">The path inside of the game files.</param>
        /// <returns>The <see cref="FileResource"/> of the file.</returns>
        public FileResource GetFile(string path)
        {
            return this.GetFile<FileResource>(path);
        }

        /// <summary>
        /// Get a <see cref="FileResource"/> with the given path, of the given type.
        /// </summary>
        /// <typeparam name="T">The type of resource</typeparam>
        /// <param name="path">The path inside of the game files.</param>
        /// <returns>The <see cref="FileResource"/> of the file.</returns>
        public T GetFile<T>(string path) where T : FileResource
        {
            ParsedFilePath filePath = Lumina.Lumina.ParseFilePath(path);
            if (filePath == null)
                return default(T);
            Repository repository;
            return this.gameData.Repositories.TryGetValue(filePath.Repository, out repository) ? repository.GetFile<T>(filePath.Category, filePath) : default(T);
        }

        #endregion
    }
}
