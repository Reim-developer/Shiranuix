package com.reim.shiranuix;

import javafx.scene.Node;
import javafx.scene.layout.StackPane;
import javafx.stage.Stage;
import org.junit.jupiter.api.Test;
import org.testfx.api.FxRobot;
import org.testfx.framework.junit5.ApplicationExtension;
import org.testfx.framework.junit5.Start;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertTrue;

/**
 * UI tests for the main application window, driven by TestFX.
 *
 * <p>Runs against a real (or Monocle headless) JavaFX toolkit, so it
 * verifies behaviour that plain unit tests cannot: the stage is shown,
 * carries the expected title, and uses an empty root pane.</p>
 */
@org.junit.jupiter.api.extension.ExtendWith(ApplicationExtension.class)
class AppTest {

    /**
     * Called by the TestFX extension before each test with a fresh stage,
     * mirroring what {@link App#start} receives in production.
     *
     * @param stage a stage managed by TestFX
     */
    @Start
    void start(Stage stage) {
        new App().start(stage);
    }

    /**
     * The window must be visible after the application starts.
     *
     * @param robot TestFX robot injected by the extension
     */
    @Test
    void windowIsShown(FxRobot robot) {
        Stage stage = lookupStage(robot);
        assertTrue(stage.isShowing(), "Primary stage should be showing");
    }

    /**
     * The window title must match the application title.
     *
     * @param robot TestFX robot injected by the extension
     */
    @Test
    void windowHasExpectedTitle(FxRobot robot) {
        assertEquals(App.TITLE, lookupStage(robot).getTitle());
    }

    /**
     * The scene must exist and expose a StackPane root pane.
     *
     * @param robot TestFX robot injected by the extension
     */
    @Test
    void sceneUsesStackPaneRoot(FxRobot robot) {
        Stage stage = lookupStage(robot);
        assertNotNull(stage.getScene(), "Scene should be set on the stage");
        assertTrue(stage.getScene().getRoot() instanceof StackPane,
                "Root should be the StackPane created by the app");
    }

    /**
     * The scene must use the default dimensions defined by {@link App}.
     *
     * @param robot TestFX robot injected by the extension
     */
    @Test
    void sceneUsesDefaultDimensions(FxRobot robot) {
        Stage stage = lookupStage(robot);
        assertEquals(App.DEFAULT_WIDTH, stage.getScene().getWidth(), 0.5);
        assertEquals(App.DEFAULT_HEIGHT, stage.getScene().getHeight(), 0.5);
    }

    /**
     * Resolves the primary stage shown by the application.
     *
     * @param robot the TestFX robot
     * @return the {@link Stage} hosting the application's root node
     */
    private Stage lookupStage(FxRobot robot) {
        Node root = robot.lookup(".root").query();
        return (Stage) root.getScene().getWindow();
    }
}
