using System;
using System.Reflection;

using Nini.Config;
using log4net;

using OpenMetaverse;

namespace OpenSim.Framework
{
    public class AssetPermissions
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(
            MethodBase.GetCurrentMethod()?.DeclaringType);

        private bool[] m_DisallowExport, m_DisallowImport;
        private string[] m_AssetTypeNames;

        public AssetPermissions(IConfig config)
        {
            var enumType = typeof(AssetType);
            m_AssetTypeNames = Enum.GetNames(enumType);
            for (var i = 0; i < m_AssetTypeNames.Length; i++)
                m_AssetTypeNames[i] = m_AssetTypeNames[i].ToLower();
            var n = Enum.GetValues(enumType).Length;
            m_DisallowExport = new bool[n];
            m_DisallowImport = new bool[n];

            LoadPermsFromConfig(config, "DisallowExport", m_DisallowExport);
            LoadPermsFromConfig(config, "DisallowImport", m_DisallowImport);

        }

        private void LoadPermsFromConfig(IConfig assetConfig, string variable, bool[] bitArray)
        {
            if (assetConfig == null)
                return;

            var perms = assetConfig.GetString(variable, string.Empty);
            var parts = perms.Split([','], StringSplitOptions.RemoveEmptyEntries);
            foreach (var s in parts)
            {
                var index = Array.IndexOf(m_AssetTypeNames, s.Trim().ToLower());
                if (index >= 0)
                    bitArray[index] = true;
                else
                    m_log.Warn($"[Asset Permissions]: Invalid AssetType {s}");
            }

        }

        public bool AllowedExport(sbyte type)
        {
            var assetTypeName = ((AssetType)type).ToString();

            var index = Array.IndexOf(m_AssetTypeNames, assetTypeName.ToLower());
            if (index < 0 || !m_DisallowExport[index]) 
                return true;
            
            m_log.Debug($"[Asset Permissions]: Export denied: configuration does not allow export of AssetType {assetTypeName}");
            return false;

        }

        public bool AllowedImport(sbyte type)
        {
            var assetTypeName = ((AssetType)type).ToString();

            var index = Array.IndexOf(m_AssetTypeNames, assetTypeName.ToLower());
            if (index < 0 || !m_DisallowImport[index]) 
                return true;
            
            m_log.Debug($"[Asset Permissions]: Import denied: configuration does not allow import of AssetType {assetTypeName}");
            return false;

        }
        
    }
}
