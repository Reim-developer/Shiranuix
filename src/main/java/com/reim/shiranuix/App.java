package com.reim.shiranuix;

import javafx.application.Application;
import javafx.scene.Scene;
import javafx.scene.layout.StackPane;
import javafx.stage.Stage;

/**
 * The main JavaFX application: currently an empty application window.
 *
 * <p>Future manga-reader features (library view, reader pane, settings)
 * will be layered on top of the {@link StackPane} root created here.</p>
 */
public class App extends Application {

    /** Default window width, in pixels. */
    static final double DEFAULT_WIDTH = 1024;

    /** Default window height, in pixels. */
    static final double DEFAULT_HEIGHT = 768;

    /** Title displayed in the window's title bar. */
    static final String TITLE = "Shiranuix";

    /**
     * Builds the scene graph and shows the primary stage.
     *
     * <p>The window is intentionally empty: it only establishes the shell
     * that subsequent features will populate.</p>
     *
     * @param stage the primary stage provided by the JavaFX runtime
     */
    @Override
    public void start(Stage stage) {
        // Empty root pane; ready for future UI components.
        StackPane root = new StackPane();

        Scene scene = new Scene(root, DEFAULT_WIDTH, DEFAULT_HEIGHT);

        stage.setTitle(TITLE);
        stage.setScene(scene);
        stage.show();
    }

    /**
     * Called when the application is about to exit.
     *
     * <p>Reserved for future cleanup of resources (open readers, HTTP
     * clients, persisted state).</p>
     */
    @Override
    public void stop() {
        // Nothing to clean up yet.
    }
}
