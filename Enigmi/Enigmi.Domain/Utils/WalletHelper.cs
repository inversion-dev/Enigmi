using System.Text;
using CardanoSharp.Wallet.Extensions.Models;
using CardanoSharp.Wallet.Models.Keys;
using CardanoSharp.Wallet.Utilities;
using Enigmi.Common;
using PeterO.Cbor2;

namespace Enigmi.Domain.Utils;
public static class WalletHelper
{
    public static bool VerifyWalletSignature(string addressHex, string payload, string keyHex, string signatureHex)
    {
        addressHex.ThrowIfNullOrWhitespace();
        payload.ThrowIfNullOrWhitespace();
        keyHex.ThrowIfNullOrWhitespace();
        signatureHex.ThrowIfNullOrWhitespace();
        
        var key = CBORObject.DecodeFromBytes(Convert.FromHexString(keyHex));
        var pubKeyBytes = key[-2].ToObject<byte[]>();

        var signatureCbor = CBORObject.DecodeFromBytes(Convert.FromHexString(signatureHex));
        var headers = signatureCbor[0].ToObject<byte[]>();
        var payloadBytes = signatureCbor[2].ToObject<byte[]>();
        var signature = signatureCbor[3].ToObject<byte[]>();

        if (!payloadBytes.SequenceEqual(Encoding.UTF8.GetBytes(payload)))
        {
            return false;
        }

        if (!VerifyAddressForPubKey(addressHex, pubKeyBytes))
        {   
            return false;
        }

        var pubKey = new PublicKey(pubKeyBytes, Array.Empty<byte>());

        var sigStructure = CBORObject.NewArray()
            .Add("Signature1")
            .Add(headers)
            .Add(Array.Empty<byte>())
            .Add(payloadBytes);

        var sigStructureBytes = sigStructure.EncodeToBytes();

        if (!pubKey.Verify(sigStructureBytes, signature))
        {
            return false;
        }

        return true;
    }

    static bool VerifyAddressForPubKey(string addressHex, byte[] pubKeyBytes)
    {
        addressHex.ThrowIfNullOrWhitespace();
        pubKeyBytes.ThrowIfNullOrEmpty();
        
        var addressBytes = Convert.FromHexString(addressHex);
        var paymentKeyHash = addressBytes.Skip(1).Take(28).ToArray();
        var pubKeyHash = HashUtility.Blake2b224(pubKeyBytes);
        return paymentKeyHash.SequenceEqual(pubKeyHash);
    }
}