using System.Collections.Generic;
using Civic.Core.Configuration;
using Civic.Core.Security;

namespace Civic.Core.Data
{
    public class DataConfig : NamedConfigurationElement
    {
        protected const string DEFAULT_CONNECTION_STRING = "default";
        private static CivicSection _coreConfig;
        private static DataConfig _current;
        private string _default;
        private Dictionary<string, string> _claimsDefaults;

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

        public string GetConnectionName(string name)
        {
            var connectionName = name;

            if (Children.ContainsKey("connection"))
            {
                var attributes = Children["connection"].Attributes;
                connectionName = attributes.ContainsKey(name) ? attributes[name] : _default;
            } else if (!string.IsNullOrEmpty(_default)) return _default;

            return string.IsNullOrEmpty(connectionName) ? name : connectionName;
        }

        public string GetSchemaName(string name)
        {
            var schemaName = name;

            if (Children.ContainsKey("schema"))
            {
                var attributes = Children["schema"].Attributes;
                schemaName = attributes.ContainsKey(name) ? attributes[name] : null;
            }
            return string.IsNullOrEmpty(schemaName) ? name : schemaName;
        }

        public Dictionary<string,string> GetClaimsDefaults()
        {
            if (_claimsDefaults != null) return _claimsDefaults;

            if (Children.ContainsKey("claims"))
            {
                var claims = Children["claims"];

                var defaults = new Dictionary<string, string>();

                foreach (var pairs in claims.Children)
                {
                    defaults[pairs.Value.Attributes["name"]] = pairs.Value.Attributes["claim"];
                }

                _claimsDefaults = defaults;
            }
            else
            {
                var defaults = new Dictionary<string, string>();

                defaults["@ouid"] = StandardClaimTypes.ORGANIZATION_ID;
                defaults["@who"] = StandardClaimTypes.PERSON_ID;

                _claimsDefaults = defaults;
            }
            return _claimsDefaults;
        }
    }
}

