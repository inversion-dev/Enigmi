var TabVisiblityHandlerReference = null;

document.addEventListener("visibilitychange", function () {  
    if (TabVisiblityHandlerReference) {
        TabVisiblityHandlerReference.invokeMethodAsync("DocumentVisibilityChanged", !document.hidden);    
    }    
});

function registerJavascriptTabVisibilityHandlerReference(tabVisiblityHandlerReference) {
    TabVisiblityHandlerReference = tabVisiblityHandlerReference
}