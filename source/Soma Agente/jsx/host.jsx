/* Soma Agente: loads the bridge for the Adobe program that opened the panel. */
(function () {
    var bridges = { aftereffects: true, photoshop: true, illustrator: true, premierepro: true };
    var id = BridgeTalk.appName;
    if (!bridges[id]) { return; }
    var here = new File($.fileName).parent;
    $.evalFile(new File(here.fsName + "/hosts/" + id + ".jsx"));
}());
