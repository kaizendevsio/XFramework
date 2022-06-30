export function beforeStart(options, extensions) {
    console.log("Blazor initializing..");
}

export function afterStarted(blazor) {
    console.log("Blazor initialized.");
    console.log("Loading JS Libs..");

    App.LoadJsFiles(App.JsLibs());
    if ('serviceWorker' in navigator) {
        navigator.serviceWorker.register('service-worker.js').then((registration) => {
        console.log('Service worker registration started');      
        }).catch(function(error) {
            console.log('Service worker registration failed');
        });
    }    
}