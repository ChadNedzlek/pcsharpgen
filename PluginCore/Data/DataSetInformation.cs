using System;
using System.Collections.Immutable;

namespace Primordially.PluginCore.Data
{
    public class DataSetInformation
    {
        public DataSetInformation(
            string name,
            DataSourceInformation sourceInformation,
            string gameMode,
            ImmutableList<string> bookTypes,
            ImmutableList<string> types,
            string status,
            string copyright,
            string description,
            string genre,
            string infoText,
            string helpUrl,
            bool isMature,
            bool isOgl,
            bool isLicensed,
            Predicate<DataSetInformation> conditions,
            DataSetPublisherInformation publisherInfo,
            int rang,
            bool showInMenu,
            string setting,
            ImmutableList<DataSetLink> lInks)
        {
            Name = name;
            SourceInformation = sourceInformation;
            GameMode = gameMode;
            BookTypes = bookTypes;
            Types = types;
            Status = status;
            Copyright = copyright;
            Description = description;
            Genre = genre;
            InfoText = infoText;
            HelpUrl = helpUrl;
            IsMature = isMature;
            IsOgl = isOgl;
            IsLicensed = isLicensed;
            Conditions = conditions;
            PublisherInfo = publisherInfo;
            Rang = rang;
            ShowInMenu = showInMenu;
            Setting = setting;
            LInks = lInks;
        }

        public string Name { get; }
        public DataSourceInformation SourceInformation { get; }
        public string GameMode { get; }
        public ImmutableList<string> BookTypes { get; }
        public ImmutableList<string> Types { get; }
        public string Status { get; }
        public string Copyright { get; }
        public string Description { get; }
        public string Genre { get; }
        public string InfoText { get; }
        public string HelpUrl { get; }
        public bool IsMature { get; }
        public bool IsOgl { get; }
        public bool IsLicensed { get; }
        public Predicate<DataSetInformation> Conditions { get; }
        public DataSetPublisherInformation PublisherInfo { get; }
        public int Rang { get; }
        public bool ShowInMenu { get; }
        public string Setting { get; }
        public ImmutableList<DataSetLink> LInks { get; }
    }
}