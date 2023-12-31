﻿using Enigmi.Blazor.Events;
using Enigmi.Blazor.Shared.Models;
using Enigmi.Blazor.Utils;
using Enigmi.Common;
using Enigmi.Messages.SignalRMessage;
using Enigmi.Messages.UserWallet;
using Microsoft.AspNetCore.Components;
using System.Collections.Generic;
using static System.FormattableString;
using Models = Enigmi.Blazor.Shared.Models;

namespace Enigmi.Blazor.Pages;

public partial class Puzzles : IDisposable
{
    [Inject]
    private ApiClient ApiClient { get; set; } = null!;
    
    [Inject]
    private AuthenticationClient AuthenticationClient { get; set; } = null!;

    [Inject]
    private OnShowBuyPuzzlePieceSectionRequestedEvent OnShowBuyPuzzlePieceSectionRequestedEvent { get; set; } = null!;
    
    [Inject]
    private OnHideBuyPuzzlePieceSectionRequestedEvent OnHideBuyPuzzlePieceSectionRequestedEvent { get; set; } = null!;

    [Inject] 
    private OnUserWalletStateRefreshedEvent OnUserWalletStateRefreshedEvent { get; set; } = null!;

    [Inject] 
    private OnUserWalletStateReceivedEvent OnUserWalletStateReceivedEvent { get; set; } = null!;

    [Inject] 
    private WalletConnection WalletConnection { get; set; } = null!;

    [Inject]
    private PuzzleSelectionManager PuzzleSelectionManager { get; set; } = null!;

    [Inject]
    private SignalRClient SignalRClient { get; set; } = null!;

    [Inject]
    private ActivePuzzlePieceUpdatedEvent ActivePuzzlePieceUpdatedEvent { get; set; } = null!;    

    private List<Models.PuzzlePieceDefinition> AvailablePuzzlePieceDefinitions = new();

    public bool IsBuyPuzzlePieceVisible { get; set; } = false;

    private bool ViewPuzzlePiecesVisible { get; set; } = true;

    private bool ViewLeftoverPuzzlePiecesVisible { get; set; } = false;

    private List<UserPuzzle> UserPuzzles { get; set; } = new();

    private List<Puzzle> CompletedPuzzles { get; set; } = new();

    private List<UserPuzzle> LeftoverPuzzles { get; set; } = new();

    private IDisposable? ActivePuzzlePieceUpdateSubscription { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        await GetWalletState().ContinueOnAnyContext();
        OnShowBuyPuzzlePieceSectionRequestedEvent.Subscribe(ClientEventUtil_OnShowBuyPuzzlePieceSectionRequested);
        OnHideBuyPuzzlePieceSectionRequestedEvent.Subscribe(ClientEventUtilOnOnHideBuyPuzzlePieceSectionRequested);
        
        OnUserWalletStateRefreshedEvent.Subscribe(ClientEventUtilOnOnUserWalletStateRefreshed);
        AuthenticationClient.OnAuthenticationStateChanged += AuthenticationStateChanged;

        ActivePuzzlePieceUpdateSubscription = SignalRClient.On((ActivePuzzlePieceUpdate message) => UpsertActivePuzzlePiece(message));
    }

    private void UpsertActivePuzzlePiece(ActivePuzzlePieceUpdate message)
    {
        Console.WriteLine(Invariant($"UpsertActivePuzzlePiece : {message}"));

        var existingPiece = AvailablePuzzlePieceDefinitions.SingleOrDefault(x => x.Id == message.PuzzlePieceDefinitionId);
        
        var updatedUserPuzzlePiece = new Models.PuzzlePieceDefinition
        {
            Id = message.PuzzlePieceDefinitionId,
            PuzzleDefinitionId = message.PuzzleDefinitionId,
            PuzzlePieceCount = message.PuzzlePieceCount,
            UtcTimestamp = message.UtcTimestamp,
            X = message.X,
            Y = message.Y
        };

        if (existingPiece == null)
        {
            AvailablePuzzlePieceDefinitions.Add(updatedUserPuzzlePiece);            
        }
        else if (message.UtcTimestamp > existingPiece.UtcTimestamp)
        {
            AvailablePuzzlePieceDefinitions.Remove(existingPiece);
            AvailablePuzzlePieceDefinitions.Add(updatedUserPuzzlePiece);
        }
        else
        {
            return;
        }

        var userPuzzle = this.UserPuzzles.SingleOrDefault(x => x.PuzzleId == updatedUserPuzzlePiece.PuzzleDefinitionId);
        if (userPuzzle == null && message.OwnedPuzzlePieceCount > 0)
        {
           var notLoadedPuzzle = this.PuzzleSelectionManager.AllPuzzles.SingleOrDefault(x => x.PuzzleId == updatedUserPuzzlePiece.PuzzleDefinitionId);
            if (notLoadedPuzzle != null)
            {
                userPuzzle = notLoadedPuzzle;
                this.UserPuzzles.Add(userPuzzle);
            }
        }

        if (userPuzzle != null)
        {
            var puzzlePiece = userPuzzle.PuzzlePieces.SingleOrDefault(x => x.PuzzlePieceDefinitionId == updatedUserPuzzlePiece.Id);
            if (puzzlePiece != null && puzzlePiece.PuzzlePiece != null)
            {
                puzzlePiece.SetOwnedCount(message.OwnedPuzzlePieceCount);                
                puzzlePiece.PuzzlePiece.UpdateAvailablePuzzlePieceCount(message.PuzzlePieceCount - message.OwnedPuzzlePieceCount);
                puzzlePiece.PuzzlePiece.UpdateIsOwnd(message.OwnedPuzzlePieceCount > 0);
                ActivePuzzlePieceUpdatedEvent.Trigger();
            }
        }
    }

    private void ClientEventUtilOnOnHideBuyPuzzlePieceSectionRequested(object? sender, EventArgs e)
    {
        IsBuyPuzzlePieceVisible = false;
        StateHasChanged();
    }

    private async void ClientEventUtilOnOnUserWalletStateRefreshed(object? sender, EventArgs e)
    {
        await GetWalletState();
    }   

    private void ClientEventUtil_OnShowBuyPuzzlePieceSectionRequested(object? sender, EventArgs e)
    {
        IsBuyPuzzlePieceVisible = true;
        StateHasChanged();
    }
    
    private void AuthenticationStateChanged(object? sender, EventArgs e)
    {
        StateHasChanged();
    }

    private async Task GetWalletState()
    {   
        if (WalletConnection.WalletConnector == null)
        {
            return;
        }
        
        var stakeAddress = WalletConnection.SelectedStakingAddress;
        Console.WriteLine("Get wallet state request ...");
        var walletState = await ApiClient.SendAsync(new GetStateRequest(stakeAddress, null));
        if (walletState == null)
        {
            return;
        }

        var ownedPuzzlePieceCount = walletState.PuzzlePieces.Where(x => x.IsOwned).Sum(x => x.OwnedPuzzlePieceIds.Count);
        OnUserWalletStateReceivedEvent.Trigger(ownedPuzzlePieceCount);

        var newPuzzleList = new List<UserPuzzle>();

        foreach (var puzzleDefinition in walletState.PuzzleDefinitions)
        {
            var userPuzzlePieces = new List<UserPuzzlePiece>();
            foreach (var puzzlePiece in walletState.PuzzlePieces.Where(x => x.PuzzleDefinitionId == puzzleDefinition.Id))
            {
                var userPuzzlePiece = new UserPuzzlePiece(puzzlePiece.PuzzlePieceDefinitionId);
                var puzzlePiecesAvailable = puzzlePiece.PuzzlePieceCount - puzzlePiece.OwnedPuzzlePieceIds.Count;

                userPuzzlePiece.SetPuzzlePiece(new PuzzlePiece(puzzlePiece.PuzzlePieceDefinitionId, puzzleDefinition.Id, puzzlePiece.ImageUrl, 0, puzzlePiece.IsOwned, puzzlePiecesAvailable));

                if (puzzlePiece.IsOwned)
                {                    
                    userPuzzlePiece.SetOwnedCount(puzzlePiece.OwnedPuzzlePieceIds.Count);
                }                

                userPuzzlePieces.Add(userPuzzlePiece);
            }

            var userPuzzle = new UserPuzzle(
                puzzleDefinition.Id, 
                puzzleDefinition.Title, 
                puzzleDefinition.PuzzleCollectionTitle, 
                puzzleDefinition.NumberOfAllowedBuilds, 
                puzzleDefinition.NumberOfCompletedBuilds, 
                puzzleDefinition.PuzzleSize, 
                userPuzzlePieces);

            newPuzzleList.Add(userPuzzle);
        }

        UserPuzzles.Clear();
        Console.WriteLine("Refreshed user puzzles");
        UserPuzzles.AddRange(newPuzzleList);        
        await PuzzleSelectionManager.UpdateUserPuzzles(UserPuzzles);
        StateHasChanged();
    }

    public void Dispose()
    {
        AuthenticationClient.OnAuthenticationStateChanged -= AuthenticationStateChanged;
        OnUserWalletStateRefreshedEvent.UnSubscribe(ClientEventUtilOnOnUserWalletStateRefreshed);
        OnShowBuyPuzzlePieceSectionRequestedEvent.UnSubscribe(ClientEventUtil_OnShowBuyPuzzlePieceSectionRequested);
        OnHideBuyPuzzlePieceSectionRequestedEvent.UnSubscribe(ClientEventUtilOnOnHideBuyPuzzlePieceSectionRequested);
        ActivePuzzlePieceUpdateSubscription?.Dispose();
    }
}
