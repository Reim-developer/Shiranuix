package com.reim.shiranuix.ui;

import com.reim.shiranuix.ui.home.HomeController;
import com.reim.shiranuix.ui.home.HomeViewModel;
import com.reim.shiranuix.ui.reader.ReaderController;
import com.reim.shiranuix.ui.reader.ReaderViewModel;
import java.io.IOException;
import java.io.UncheckedIOException;
import java.util.Objects;
import javafx.fxml.FXMLLoader;
import javafx.scene.Parent;

/**
 * Loads the FXML screens and wires each one to its view model.
 *
 * <p>Centralising the loading here keeps the bootstrapping code in
 * {@code App} small and makes the wiring (which controller gets which view
 * model) explicit and easy to review.</p>
 */
public final class ViewFactory {

    /** Builds a screen: loads its FXML, wires its view model, returns the root. */
    public interface ScreenFactory {
        /**
         * Constructs the screen.
         *
         * @return the root node of the loaded screen
         */
        Parent create();
    }

    private final HomeViewModel homeViewModel;
    private final ReaderViewModel readerViewModel;

    /**
     * Creates a factory wired to the given view models.
     *
     * @param homeViewModel   view model for the home screen
     * @param readerViewModel view model for the reader screen
     */
    public ViewFactory(HomeViewModel homeViewModel, ReaderViewModel readerViewModel) {
        this.homeViewModel = Objects.requireNonNull(homeViewModel, "homeViewModel");
        this.readerViewModel = Objects.requireNonNull(readerViewModel, "readerViewModel");
    }

    /**
     * @return a factory that loads the home screen
     */
    public ScreenFactory home() {
        return () -> {
            FXMLLoader loader = load("/fxml/home-view.fxml");
            HomeController controller = loader.getController();
            controller.setViewModel(homeViewModel);
            return loader.getRoot();
        };
    }

    /**
     * @return a factory that loads the reader screen
     */
    public ScreenFactory reader() {
        return () -> {
            FXMLLoader loader = load("/fxml/reader-view.fxml");
            ReaderController controller = loader.getController();
            controller.setViewModel(readerViewModel);
            return loader.getRoot();
        };
    }

    /**
     * Loads an FXML resource.
     *
     * @param location classpath path of the FXML file
     * @return the configured loader (controller already instantiated)
     * @throws UncheckedIOException if the FXML cannot be loaded
     */
    private static FXMLLoader load(String location) {
        FXMLLoader loader = new FXMLLoader(ViewFactory.class.getResource(location));
        try {
            loader.load();
        } catch (IOException e) {
            throw new UncheckedIOException("Failed to load FXML: " + location, e);
        }
        return loader;
    }
}
