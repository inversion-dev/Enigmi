using CardanoSharp.Wallet;
using CardanoSharp.Wallet.Extensions.Models;
using CardanoSharp.Wallet.Models.Keys;
using CardanoSharp.Wallet.TransactionBuilding;
using CardanoSharp.Wallet.Utilities;
using Enigmi.Common;
using Enigmi.Common.Domain;
using Enigmi.Common.Messaging;
using Enigmi.Domain.Entities.PolicyListAggregate.Events;
using Enigmi.Domain.Entities.PolicyListAggregate.ValueObjects;
using Newtonsoft.Json;
using static System.FormattableString;
using ApplicationException = Enigmi.Common.Exceptions.ApplicationException;

namespace Enigmi.Domain.Entities.PolicyListAggregate;

public class PolicyList : DomainEntity
{
    private List<PolicyToPuzzleCollectionMap> _policies = new();

    public PolicyList(int id)
    {
        Id = id;
    }

    [JsonConstructor]
    private PolicyList()
    {
        
    }

    [JsonProperty]
    public int Id { get; private set; }

    [JsonProperty]
    public IEnumerable<PolicyToPuzzleCollectionMap> Policies
    {
        get { return _policies.AsReadOnly(); }
        private set { _policies = value.ToList(); }
    }

    public ResultOrError<Enigmi.Constants.Unit> AddPolicy(Guid puzzleCollectionId, string policyIdString, PolicyType policyType)
    {
        if (_policies.Any(x => x.PuzzleCollectionId == puzzleCollectionId
                               && x.PolicyType == policyType))
        {
            "Policy type for collection already exists".ToFailedResponse<Enigmi.Constants.Unit>();
        }
        
        _policies.Add(new PolicyToPuzzleCollectionMap(
                puzzleCollectionId.ThrowIfEmpty(),
                policyIdString.ThrowIfNullOrWhitespace(),
                policyType
                )
        );

        return new Enigmi.Constants.Unit().ToSuccessResponse();
    }

    public void MarkAsAdded(string policyId)
    {
        var policy = _policies.FirstOrDefault(x => x.PolicyId == policyId.ThrowIfNullOrWhitespace());
        if (policy == null)
        {
            throw new ApplicationException(Invariant($"PolicyId '{policyId}'"));
        }
        
        policy.PolicyStatus = PolicyStatus.Added;
        RaiseEvent(new PolicyAdded(policy.PolicyType, policyId));
    }

    public (Mnemonic mnemonic, string policyIdString, uint policyClosingSlot) GeneratePolicy(DateTime policyClosingUtcDate, uint latestSlot)
    {
        IMnemonicService service = new MnemonicService();
        Mnemonic mnemonic = service.Generate(24);
        PrivateKey rootKey = mnemonic.GetRootKey("");
        var privateKey = rootKey.Derive("m/1852'/1815'/0'/0/0");
        var publicKey = GetPublicKeyFixed(privateKey);

        var publicKeyHash = HashUtility.Blake2b224(publicKey.Key);

        var invalidAfterSlot = GetSlotForDateTime(policyClosingUtcDate, latestSlot);

        var policySignScript = NativeScriptBuilder.Create
            .SetKeyHash(publicKeyHash)
            .SetInvalidAfter(invalidAfterSlot);

        var policyScriptBuilder = ScriptAllBuilder.Create
            .SetScript(policySignScript);

        var policyScript = policyScriptBuilder.Build();

        var policyId = policyScript.GetPolicyId();

        var policyIdString = Convert.ToHexString(policyId).ToLowerInvariant();
        return (mnemonic,policyIdString, invalidAfterSlot);
    }
    
    private uint GetSlotForDateTime(DateTime dateTime, uint latestSlot)
    {
        var timeDifference = dateTime - DateTime.UtcNow;
        return latestSlot + (uint)timeDifference.TotalSeconds;
    }
    
    private static PublicKey GetPublicKeyFixed(PrivateKey privateKey)
    {
        var pk = privateKey.GetPublicKey();
        if (pk.Key.Length == 33)
        {
            pk = new PublicKey(pk.Key.Skip(1).ToArray(), pk.Chaincode);
        }

        return pk;
    }
}