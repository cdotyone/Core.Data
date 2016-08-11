using Civic.Core.Configuration;

namespace Civic.Core.Data
{
    public class DataConfig : NamedConfigurationElement
    {
        protected const string DEFAULT_CONNECTION_STRING = "default";
        private static CivicSection _coreConfig;
        private static DataConfig _current;
        private string _default;

        public DataConfig(INamedElement element)
        {
            if (element == null) element = new NamedConfigurationElement() {Name = SectionName };
            Children = element.Children;
            Attributes = element.Attributes;
            Name = element.Name;

            if(Attributes.ContainsKey(DEFAULT_CONNECTION_STRING)) _default = Attributes[DEFAULT_CONNECTION_STRING];
        }

        /// <summary>
        /// The current configuration for the audit library
        /// </summary>
        public static DataConfig Current
        {
            get
            {
                if (_current != null) return _current;

                if (_coreConfig == null) _coreConfig = CivicSection.Current;
                _current = new DataConfig(_coreConfig.Children.ContainsKey(SectionName) ? _coreConfig.Children[SectionName] : null);
                return _current;
            }
        }

        /// <summary>
        /// The name of the configuration section.
        /// </summary>
        public static string SectionName
        {
            get { return "data"; }
        }

        public string GetConnectionStringName(string name)
        {
            var connectionName = Attributes.ContainsKey(name) ? Attributes[name] : _default;
            if (string.IsNullOrEmpty(connectionName)) return name;
            return connectionName;
        }
    }
}

