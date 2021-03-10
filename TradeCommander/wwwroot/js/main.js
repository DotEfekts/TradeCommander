(function () {
    document.onmouseup = function () {
        var input = document.getElementById('command-input');
        if (input)
            input.focus();
    }
})();