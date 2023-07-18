using Enigmi.Common.Messaging;

namespace Enigmi.Grains.Shared.UserWallet.Messages;

public record ApproveOrderCommand(Guid OrderId, string WitnessCbor);

public record ApproveOrderResponse; 

