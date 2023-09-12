let animatingQuadPuzzleIds = [];

function animateTradingPuzzlePieces(elementContainerId) {
    
    if (animatingQuadPuzzleIds.some(x => x == elementContainerId))
    {
    
        return;
    }
        
    animatingQuadPuzzleIds.push(elementContainerId);

    try {

        let ownerAnimateFrom = document.querySelector(`#${elementContainerId} .owner-animate-from`);
        let ownerAnimateTo = document.querySelector(`#${elementContainerId} .owner-animate-to`);

        let counterpartyAnimateFrom = document.querySelector(`#${elementContainerId} .counter-party-animate-from`);
        let counterpartyAnimateTo = document.querySelector(`#${elementContainerId} .counter-party-animate-to`);       

        const ownerClone = ownerAnimateTo.cloneNode(true);
        const counterpartyClone = counterpartyAnimateTo.cloneNode(true);
        
        applyStyleChangesForAnimation(ownerClone, ownerAnimateFrom)
        applyStyleChangesForAnimation(counterpartyClone, counterpartyAnimateFrom)

        let containers = document.querySelectorAll(`#${elementContainerId} .puzzle-pieces`);
        containers[1].appendChild(ownerClone);
        containers[2].appendChild(counterpartyClone);

        
        if (ownerAnimateFrom.classList.contains('animate-without-colour')){
            ownerAnimateFrom.classList.add('not-owned');
        }

        if (ownerAnimateTo.classList.contains('animate-without-colour')) {
            ownerAnimateTo.classList.add('not-owned');
        }

        if (counterpartyAnimateFrom.classList.contains('animate-without-colour')) {
            counterpartyAnimateFrom.classList.add('not-owned');
        }

        if (counterpartyAnimateTo.classList.contains('animate-without-colour')) {
            counterpartyAnimateTo.classList.add('not-owned');
        }


        let counterPartyLeftOffSet = counterpartyAnimateFrom.getBoundingClientRect().left - containers[2].getBoundingClientRect().left;
        let counterPartyTopOffSet = (counterpartyAnimateFrom.getBoundingClientRect().top) - containers[2].getBoundingClientRect().top;

        let ownerLeftOffSet = ownerAnimateFrom.getBoundingClientRect().left - containers[1].getBoundingClientRect().left;
        let ownerTopOffSet = (ownerAnimateFrom.getBoundingClientRect().top) - containers[1].getBoundingClientRect().top;

        counterpartyClone.style.left = counterPartyLeftOffSet + "px";
        counterpartyClone.style.top = counterPartyTopOffSet + "px";

        ownerClone.style.left = ownerLeftOffSet + "px";
        ownerClone.style.top = ownerTopOffSet + "px";
     
        setTimeout(function () {
            
            counterpartyClone.style.left = (counterPartyLeftOffSet + counterpartyAnimateTo.getBoundingClientRect().left + window.scrollX) - (counterpartyAnimateFrom.getBoundingClientRect().left + window.scrollX) + "px";
            ownerClone.style.left = (ownerLeftOffSet + ownerAnimateTo.getBoundingClientRect().left + window.scrollX) - (ownerAnimateFrom.getBoundingClientRect().left + window.scrollX) + "px";
            

            setTimeout(function () {                
                
                containers[2].removeChild(counterpartyClone);
                containers[1].removeChild(ownerClone);
                
                animatingQuadPuzzleIds.shift();

                if (ownerAnimateFrom.classList.contains('animate-without-colour')) {
                    ownerAnimateFrom.classList.remove('not-owned');
                }

                if (ownerAnimateTo.classList.contains('animate-without-colour')) {
                    ownerAnimateTo.classList.remove('not-owned');
                }

                if (counterpartyAnimateFrom.classList.contains('animate-without-colour')) {
                    counterpartyAnimateFrom.classList.remove('not-owned');
                }

                if (counterpartyAnimateTo.classList.contains('animate-without-colour')) {
                    counterpartyAnimateTo.classList.remove('not-owned');
                }

            }, 2000);

        }, 500);
    }
    catch (err) {
        console.log(err);
    }
}

function applyStyleChangesForAnimation(ownerClone, ownerAnimateFrom) {
    ownerClone.style.position = 'absolute';
    ownerClone.classList.add('animation-item');
    ownerClone.style.zIndex = 1;
    ownerClone.style.height = ownerAnimateFrom.offsetHeight + "px";
    ownerClone.style.width = ownerAnimateFrom.offsetWidth + "px";
}