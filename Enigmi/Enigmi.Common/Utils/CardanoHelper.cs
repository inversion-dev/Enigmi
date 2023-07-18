using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Enigmi.Common.Utils;

public static class CardanoHelper
{
    public static ulong ConvertToLovelace(decimal valueAda)
    {
        return (ulong)(Constants.LovelacePerAda * valueAda);
    }

    public static decimal ConvertLovelaceToAda(this ulong lovelace)
    {
        return Convert.ToDecimal(lovelace) / Constants.LovelacePerAda;
    }

    [return: NotNullIfNotNull("policyId")]
    public static string? GetAssetId(byte[]? policyId, string? assetName)
    {
        return policyId == null || assetName == null ? null :
            policyId.ToHexStringLower() + ConvertAssetNameToBytes(assetName).ToHexStringLower();
    }

    [return: NotNullIfNotNull("policyId")]
    public static string? GetAssetId(byte[]? policyId, byte[]? assetName)
    {
        return policyId == null || assetName == null ? null :
            policyId.ToHexStringLower() + assetName.ToHexStringLower();
    }

    [return: NotNullIfNotNull("policyId")]
    public static string? GetAssetId(string? policyId, string? assetName)
    {
        return policyId == null || assetName == null ? null :
            policyId.ToLowerInvariant() + ConvertAssetNameToBytes(assetName).ToHexStringLower();
    }

    public static string ConvertAssetNameToString(byte[] assetName)
    {
        return Encoding.UTF8.GetString(assetName.ThrowIfNull());
    }

    public static byte[] ConvertAssetNameToBytes(string assetName)
    {
        return Encoding.UTF8.GetBytes(assetName.ThrowIfNullOrWhitespace());
    }

    public static (byte[] PolicyId, byte[] AssetName) ConvertAssetIdToPolicyIdAndAssetName(string assetId)
    {
        assetId.ThrowIfNullOrWhitespace();
        var policyId = Convert.FromHexString(assetId.Substring(0, 56));
        var assetName = Convert.FromHexString(assetId.Substring(56));

        return (policyId, assetName);
    }

    public static (string policyId, string assetName) ConvertAssetIdToPolicyIdAndAssetNameString(string assetId)
    {
        assetId.ThrowIfNullOrWhitespace();
        var policyId = assetId.Substring(0, 56);
        var assetName = ConvertAssetNameToString(Convert.FromHexString(assetId.Substring(56)));

        return (policyId, assetName);
    }

    public static string ToHexStringLower(this byte[] array)
    {
        array.ThrowIfNullOrEmpty();
        return Convert.ToHexString(array).ToLowerInvariant();
    }
}