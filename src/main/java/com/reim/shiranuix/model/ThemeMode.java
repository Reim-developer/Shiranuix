package com.reim.shiranuix.model;

/**
 * Application colour theme.
 *
 * <p>{@link #SYSTEM} resolves to light or dark based on the host OS
 * preference. The reader page background is controlled separately from the
 * chrome theme so that pages can always sit on a neutral backdrop.</p>
 */
public enum ThemeMode {

    /** Follow the operating system's light/dark preference. */
    SYSTEM("System"),

    /** Always use the light theme. */
    LIGHT("Light"),

    /** Always use the dark theme. */
    DARK("Dark");

    private final String label;

    ThemeMode(String label) {
        this.label = label;
    }

    /**
     * Human-readable label for use in the UI.
     *
     * @return the display label
     */
    public String getLabel() {
        return label;
    }
}
