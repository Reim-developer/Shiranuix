package com.reim.shiranuix;

import javafx.application.Application;

/**
 * Launcher entry point for the Shiranuix manga reader.
 *
 * <p>This class exists separately from {@link App} because the JavaFX
 * launcher refuses to start an application whose main class extends
 * {@link Application} when running from the class path. Delegating to
 * {@link Application#launch} from a plain main class avoids that pitfall.</p>
 */
public final class Main {

    /** Utility class: not instantiable. */
    private Main() {
    }

    /**
     * Program entry point.
     *
     * @param args command-line arguments forwarded to the JavaFX runtime
     */
    public static void main(String[] args) {
        Application.launch(App.class, args);
    }
}
