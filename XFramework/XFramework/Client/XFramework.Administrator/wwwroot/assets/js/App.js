$.ajaxSetup({
  cache: true
});

window.onload = function () {
    console.log("onload Done")
}
document.addEventListener("DOMContentLoaded", () => {
    console.log("DOMContentLoaded Done")
});

const App = {
    JsLibs: function () {
        let x = [
            {"Path" : "js/vendor/jquery-3.5.1.min.js"},
            {"Path" : "js/vendor/bootstrap.bundle.min.js"},
            {"Path" : "js/vendor/OverlayScrollbars.min.js"},
            {"Path" : "js/vendor/autoComplete.min.js"},
            {"Path" : "js/vendor/clamp.min.js"},
            {"Path" : "icon/acorn-icons.js"},
            {"Path" : "icon/acorn-icons-interface.js"},
            {"Path" : "icon/acorn-icons-medical.js"},
            {"Path" : "js/vendor/Chart.bundle.min.js"},
            {"Path" : "js/vendor/chartjs-plugin-rounded-bar.min.js"},
            {"Path" : "js/vendor/chartjs-plugin-crosshair.js"},
            {"Path" : "js/vendor/fullcalendar/main.min.js"},
            {"Path" : "js/cs/charts.extend.js"},
            {"Path" : "js/pages/dashboards.doctor.js"},
            {"Path" : "js/common.js"},
            {"Path" : "js/scripts.js"},
            ];
        return JSON.stringify(x);
    },
    LoadJsFiles: function (srcPaths) {
        var r = JSON.parse(srcPaths)
        const first = document.getElementsByTagName('script')[0];
        var i = 1;
        for (const srcPath of r) {
            $.getScript(srcPath.Path)
                .done(function (script, textStatus) {
                    window.App.FireOnLoadEvent(r.length, i);
                    i++;
                })
                .fail(function (jqxhr, settings, exception) {
                    console.log("Triggered ajaxError handler.");
                });
        }
        
        console.log("LoadJsFiles Done")
    },
    LoadColorTheme: function () {
        /* sidebar right color scheme */
        
        var html = $('html');
    
        if ($.cookie("layoutmode") === 'dark-mode') {
            $('#btn-layout-modes-light').prop('checked', false);
            $('#btn-layout-modes-dark').prop('checked', true);
            $('#darkmodeswitch').prop('checked', true);
            html.addClass('dark-mode');
        } else {
            $('#btn-layout-modes-light').prop('checked', true);
            $('#btn-layout-modes-dark').prop('checked', false);
            html.removeClass('dark-mode');
        }
    },    
    FocusInput: function (inputId) {
        setTimeout(function () {
            document.getElementById(inputId).focus();
        }, 100)
    },
    FireOnLoadEvent: function (totalCount, downloaded) {
        if (totalCount === downloaded){
            setTimeout(function () {
                $(window).trigger( 'load' );
                //document.dispatchEvent(new CustomEvent('DOMContentLoaded'));
                dispatchEvent(new Event('DOMContentLoaded'));
            },1)
        }
    },
    StartVideoChat: function () {
        const domain = 'meet.jit.si';
        const options = {
            roomName: 'Consulation',
            width: document.querySelector('#meet').offsetWidth,
            height: document.querySelector('#meet').offsetHeight,
            configOverwrite: config,
            parentNode: document.querySelector('#meet')
        };
        const api = new JitsiMeetExternalAPI(domain, options);

        setTimeout(function () {
            api.executeCommand('displayName', 'Consulation');
        }, 1000)
    }
}

window.App = App;