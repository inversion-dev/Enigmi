using CardanoSharp.Wallet;
using CardanoSharp.Wallet.Enums;
using CardanoSharp.Wallet.Extensions.Models;
using CardanoSharp.Wallet.Models.Addresses;
using CardanoSharp.Wallet.Models.Keys;
using CardanoSharp.Wallet.Utilities;

namespace Enigmi.Domain.Entities.SystemWalletAggregate;

public class SystemWallet : Wallet
{
    private Address? _address;
    private string MnemonicPhrase { get; } = "gain equal word blur next erosion virtual swing hammer cruel impulse cook normal yellow travel laptop unknown devote universe meat muscle clay outside dragon";

    public string GetHumanFriendlyAddress()
    {
        BuildAddress();
        return _address!.ToString();
    }

    private void BuildAddress()
    {
        if (_address != null)
        {
            return;
        }
        
        IMnemonicService service = new MnemonicService();
        Mnemonic mnemonic = service.Restore(MnemonicPhrase);
        PrivateKey rootKey = mnemonic.GetRootKey("");

        PrivateKey paymentPrv = rootKey.Derive($"m/1852'/1815'/0'/0/0");
        PublicKey paymentPub = paymentPrv.GetPublicKey(false);

        PrivateKey stakePrv = rootKey.Derive($"m/1852'/1815'/0'/2/0");
        PublicKey stakePub = stakePrv.GetPublicKey(false);

        _address = AddressUtility.GetBaseAddress(paymentPub, stakePub, NetworkType.Preprod);
    }
}