using Enigmi.Common;
using Enigmi.Common.Messaging;
using Enigmi.Messages.System;
using FluentValidation;

namespace Enigmi.Application.Handlers.System;

public class PingRequestHandler : Handler<PingRequest, PingResponse>
{
	public IClusterClient ClusterClient { get; }

	public PingRequestHandler(IClusterClient clusterClient)
	{
		ClusterClient = clusterClient.ThrowIfNull();
	}

	public override Task<ResultOrError<PingResponse>> Execute(PingRequest request, CancellationToken cancellationToken)
	{
		request.ThrowIfNull();
        var response = new PingResponse().ToSuccessResponse();
		return Task.FromResult(response);
	}
}

public class PingRequestValidator : AbstractValidator<PingRequest>
{
	public PingRequestValidator()
	{		
	}
}