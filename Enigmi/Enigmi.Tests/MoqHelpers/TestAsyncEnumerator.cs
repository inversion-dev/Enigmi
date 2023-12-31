﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Enigmi.Tests.MoqHelpers;

internal class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
{
	private readonly IEnumerator<T> _inner;

	public TestAsyncEnumerator(IEnumerator<T> inner)
	{
		_inner = inner;
	}

	public void Dispose()
	{
		_inner.Dispose();
	}

	public T Current
	{
		get
		{
			return _inner.Current;
		}
	}

	public Task<bool> MoveNext(CancellationToken cancellationToken)
	{
		return Task.FromResult(_inner.MoveNext());
	}

	public ValueTask<bool> MoveNextAsync()
	{
		return ValueTask.FromResult(_inner.MoveNext());
	}

	public ValueTask DisposeAsync()
	{
		return new ValueTask();
	}
}