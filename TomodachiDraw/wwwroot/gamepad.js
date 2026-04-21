// gamepad.js - exposes current gamepad state to Blazor
window.getGamepadState = function () {
    const gamepads = navigator.getGamepads ? navigator.getGamepads() : [];
    for (const gp of gamepads) {
        if (!gp) continue;
        return { buttons: gp.buttons.map(b => typeof b === 'object' ? b.pressed : b > 0.5), axes: Array.from(gp.axes) };
    }
    return null;
};

// Returns JSON string — avoids Blazor/Razor generic type parsing issues
window.getGamepadStateJson = function () {
    const state = window.getGamepadState();
    return state ? JSON.stringify(state) : '';
};
