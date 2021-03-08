﻿function scrollConsoleDown() {
    window.setTimeout(function() {
        const console = document.getElementById('console');
        console.scrollTop = console.scrollHeight;
    }, 0);
}

function moveCaretToEnd() {
    window.setTimeout(function () {
        const input = document.getElementById('command-input');
        input.selectionStart = input.selectionEnd = input.value.length;
    }, 0);
}

function renderMap(locations, width, height, shipData) {
    const scale = 5;

    const canvas = document.getElementById('map');
    canvas.width = width * scale;
    canvas.height = height * scale;


    if (canvas.getContext) {
        const ctx = canvas.getContext('2d');

        const xTranslate = canvas.width / 2;
        const yTranslate = canvas.height / 2;

        ctx.strokeStyle = 'lime';
        ctx.fillStyle = 'lime';
        ctx.lineWidth = 1;
        ctx.font = "18px 'Share Tech Mono'";

        ctx.beginPath();
        ctx.moveTo(0, yTranslate);
        ctx.lineTo(canvas.width, yTranslate);
        ctx.stroke();

        ctx.beginPath();
        ctx.moveTo(xTranslate, 0);
        ctx.lineTo(xTranslate, canvas.height);
        ctx.stroke();

        for (let i = 0; i < locations.length; i++) {
            let location = locations[i];

            let isLargeBody = location.type === "PLANET" || location.type === "GAS_GIANT";
            let x = location.x;
            let y = -location.y;


            ctx.beginPath();
            ctx.arc((x * scale) + xTranslate, (y * scale) + yTranslate, (isLargeBody ? 1.5 : 0.75) * scale, 0, 2 * Math.PI, false);
            if (isLargeBody)
                ctx.stroke();
            else
                ctx.fill();
        }

        let textLocations = [];
        for (let i = 0; i < locations.length; i++) {
            let location = locations[i];

            let isLargeBody = location.type === "PLANET" || location.type === "GAS_GIANT";
            let x = ((location.x + (isLargeBody ? 2.5 : 2)) * scale) + xTranslate;
            let y = (-location.y * scale) + yTranslate;

            let textSize = ctx.measureText(location.symbol);
            let textLoc = { x1: x - 1, y1: y - 1, x2: x + textSize.width + 1, y2: y + 18 + 1 };

            for (let i = 0; i < textLocations.length; i++) {
                let testLoc = textLocations[i];

                if (overlaps(textLoc, testLoc)) {
                    if(textLoc.y1 - testLoc.y1 > 0)
                        textLoc.y1 = testLoc.y2;
                    else
                        textLoc.y1 = testLoc.y1 - 19;

                    break;
                }
            }

            ctx.fillText(location.symbol, textLoc.x1 + 1, textLoc.y1 + 1);
            textLocations.push(textLoc);
        }

        for (let i = 0; i < shipData.length; i++) {
            let ship = shipData[i];
            if (ship.lastFlightPlan && ship.flightEnded === false) {
                let flightPlanStart = locations.find(l => l.symbol === ship.lastFlightPlan.departure);
                let flightPlanEnd = locations.find(l => l.symbol === ship.lastFlightPlan.destination);
                
                if (flightPlanStart && flightPlanEnd) {
                    let x1 = flightPlanStart.x;
                    let y1 = -flightPlanStart.y;
                    let x2 = flightPlanEnd.x;
                    let y2 = -flightPlanEnd.y;

                    ctx.beginPath();
                    ctx.moveTo((x1 * scale) + xTranslate, (y1 * scale) + yTranslate);
                    ctx.lineTo((x2 * scale) + xTranslate, (y2 * scale) + yTranslate);
                    ctx.stroke();

                    let shipProgress = ship.timeElapsed / (ship.timeElapsed + ship.lastFlightPlan.timeRemainingInSeconds);
                    let shipX = x1 + ((x2 - x1) * shipProgress);
                    let shipY = y1 + ((y2 - y1) * shipProgress);

                    ctx.beginPath();
                    ctx.arc((shipX * scale) + xTranslate, (shipY * scale) + yTranslate, 0.5 * scale, 0, 2 * Math.PI, false);
                    ctx.fill();

                    ctx.fillText(ship.displayName, ((shipX + 1) * scale) + xTranslate, ((shipY + 1) * scale) + yTranslate);
                }
            }
        }
    }

    function overlaps(a, b) {
        if (a.x1 >= b.x2 || b.x1 >= a.x2) return false;
        if (a.y1 >= b.y2 || b.y1 >= a.y2) return false;
        return true;
    }
}