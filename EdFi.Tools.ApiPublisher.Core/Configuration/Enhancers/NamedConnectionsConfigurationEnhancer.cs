using System;
using System.Collections.Generic;
using System.Linq;
using EdFi.Tools.ApiPublisher.Core.Management;
using log4net;
using Microsoft.Extensions.Configuration;

namespace EdFi.Tools.ApiPublisher.Core.Configuration.Enhancers
{
    public class NamedConnectionsConfigurationEnhancer : IConfigurationBuilderEnhancer
    {
        private readonly INamedApiConnectionDetailsReader _namedApiConnectionDetailsReader;

        private readonly ILog _logger = LogManager.GetLogger(typeof(NamedConnectionsConfigurationEnhancer));
        
        public NamedConnectionsConfigurationEnhancer(INamedApiConnectionDetailsReader namedApiConnectionDetailsReader)
        {
            _namedApiConnectionDetailsReader = namedApiConnectionDetailsReader;
        }

        public void Enhance(IConfigurationBuilder configurationBuilder)
        {
            // NOTE: We could pass the base configuration (or even the connections) in to avoid this step
            var currentConfiguration = configurationBuilder.Build();
            var connections = currentConfiguration.Get<ConnectionConfiguration>().Connections;

            // Get the Configuration Store section
            var configurationStoreSection = currentConfiguration.GetSection("configurationStore");

            // Add source/target connection configuration from named connections
            var additionalConfigurationValues = new List<KeyValuePair<string, string>>();
            
            // Get additional named configuration values for source, as necessary
            additionalConfigurationValues.AddRange(
                GetEnhancedConnectionConfigurationValues(connections.Source, ConnectionType.Source));
            
            // Get additional named configuration values for target, as necessary
            additionalConfigurationValues.AddRange(
                GetEnhancedConnectionConfigurationValues(connections.Target, ConnectionType.Target));

            // Add in named connections (now provided as "Source" and "Target" connections)
            var enhancedConfiguration = configurationBuilder
                .AddInMemoryCollection(additionalConfigurationValues)
                .Build();
            
            // Recheck finalized connection configurations
            var finalizedConnections = enhancedConfiguration.Get<ConnectionConfiguration>().Connections;

            if (!finalizedConnections.Source.IsFullyDefined())
            {
                throw new ArgumentException($"Source connection '{connections.Source.Name}' was not fully configured.");
            }
            
            if (!finalizedConnections.Target.IsFullyDefined())
            {
                throw new ArgumentException($"Target connection '{connections.Target.Name}' was not fully configured.");
            }

            IEnumerable<KeyValuePair<string, string>> GetEnhancedConnectionConfigurationValues(ApiConnectionDetails connection, ConnectionType connectionType)
            {
                // Get additional named configuration values for source, if necessary
                if (!connection.IsFullyDefined())
                {
                    _logger.Debug($"{connectionType.ToString()} connection details are not fully defined.");

                    if (string.IsNullOrEmpty(connection.Name))
                    {
                        throw new ArgumentException($"{connectionType.ToString()} connection details were not available and no connection name was supplied.");
                    }

                    var configurationValues = CreateNamedConnectionConfigurationValues(connection.Name, connectionType)
                        .ToArray();

                    return configurationValues;
                }
                
                return Enumerable.Empty<KeyValuePair<string, string>>();
            }
            
            IEnumerable<KeyValuePair<string, string>> CreateNamedConnectionConfigurationValues(
                string apiConnectionName,
                ConnectionType connectionType)
            {
                _logger.Debug($"Obtaining {connectionType.ToString().ToLower()} API connection details for connection '{apiConnectionName}' using '{_namedApiConnectionDetailsReader.GetType().Name}'.");

                var namedApiConnectionDetails = _namedApiConnectionDetailsReader.GetNamedApiConnectionDetails(apiConnectionName, configurationStoreSection);
                
                if (!namedApiConnectionDetails.IsFullyDefined())
                {
                    throw new ArgumentException($"Named {connectionType.ToString().ToLower()} connection '{namedApiConnectionDetails.Name}' was not configured.");
                }

                // Fill in source configuration details
                yield return new KeyValuePair<string, string>(
                    $"Connections:{connectionType.ToString()}:Url", 
                    namedApiConnectionDetails.Url);

                yield return new KeyValuePair<string, string>(
                    $"Connections:{connectionType.ToString()}:Key",
                    namedApiConnectionDetails.Key);

                yield return new KeyValuePair<string, string>(
                    $"Connections:{connectionType.ToString()}:Secret",
                    namedApiConnectionDetails.Secret);

                if (!string.IsNullOrEmpty(namedApiConnectionDetails.Scope))
                {
                    yield return new KeyValuePair<string, string>(
                        $"Connections:{connectionType.ToString()}:Scope",
                        namedApiConnectionDetails.Scope);
                }
                
                if (namedApiConnectionDetails.IgnoreIsolation.HasValue)
                {
                    yield return new KeyValuePair<string, string>(
                        $"Connections:{connectionType.ToString()}:Force",
                        namedApiConnectionDetails.IgnoreIsolation.ToString());
                }

                if (namedApiConnectionDetails.LastChangeVersionProcessedByTargetName.Any())
                {
                    yield return new KeyValuePair<string, string>(
                        $"Connections:{connectionType.ToString()}:LastChangeVersionsProcessed",
                        namedApiConnectionDetails.LastChangeVersionsProcessed);
                }

                if (!string.IsNullOrEmpty(namedApiConnectionDetails.Resources))
                {
                    yield return new KeyValuePair<string, string>(
                        $"Connections:{connectionType.ToString()}:Resources", 
                        namedApiConnectionDetails.Resources);
                }
                
                if (!string.IsNullOrEmpty(namedApiConnectionDetails.ExcludeResources))
                {
                    yield return new KeyValuePair<string, string>(
                        $"Connections:{connectionType.ToString()}:ExcludeResources", 
                        namedApiConnectionDetails.ExcludeResources);
                }

                // Treating Forbidden response as warning is only applicable for "target" connections
                if (namedApiConnectionDetails.TreatForbiddenPostAsWarning.HasValue
                    && connectionType == ConnectionType.Target)
                {
                    yield return new KeyValuePair<string, string>(
                        $"Connections:{connectionType.ToString()}:TreatForbiddenPostAsWarning", 
                        namedApiConnectionDetails.TreatForbiddenPostAsWarning.ToString());
                }
            }
        }

        private enum ConnectionType
        {
            Source,
            Target,
        }
    }
}