

const Menu = {
    CheckActivityHeader: function () {
        var bodyRect = document.body.getBoundingClientRect(),
            elemRect = document.getElementById('activity_header').getBoundingClientRect(),
            offset = elemRect.top - bodyRect.top

        if (offset < 30) {
            document.getElementById('activity_menu').classList.add("expanded");
        }
        else {
            document.getElementById('activity_menu').classList.remove("expanded");
        }
    }
}

const Activity = {
    GoBack: function () {
        window.history.back();
    }

}

const Utilities = {
    StartQR: function () {
        window.setTimeout(() => {
            let scanner = new Instascan.Scanner({ video: document.getElementById('videoElem') });
            window.scanner = scanner;
            scanner.addListener('scan', function (content) {
                console.log(content);
                scanner.stop();
                window.location.replace("user/register");
            });
            Instascan.Camera.getCameras().then(function (cameras) {
                if (cameras.length > 0) {
                    document.getElementById('videoElemAltText').style.display = 'none';
                    scanner.start(cameras[0]);
                } else {
                    console.error('No cameras found.');
                }
            }).catch(function (e) {
                console.error(e);
            });
        }, 500)
       
    },
    StopQR: function () {
        window.scanner.stop();
    }
}

const App = {
    Menu: Menu,
    Activity: Activity,
    Utilities: Utilities
}