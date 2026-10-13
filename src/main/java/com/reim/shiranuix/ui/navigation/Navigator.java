package com.reim.shiranuix.ui.navigation;

import java.util.Objects;
import java.util.function.Supplier;
import javafx.scene.Parent;
import javafx.scene.layout.StackPane;

/**
 * Swaps the single visible screen inside a shared container.
 *
 * <p>The application shows one screen at a time (home, then reader). The
 * navigator owns the container and hands each screen a way to request a
 * switch. Screens are created lazily by suppliers so nothing is built until
 * it is first shown.</p>
 */
public final class Navigator {

    /** The available application screens. */
    public enum Screen {
        HOME,
        READER
    }

    private final StackPane container;
    private final Supplier<Parent> homeFactory;
    private final Supplier<Parent> readerFactory;

    /**
     * Creates a navigator.
     *
     * @param container     the container whose child is swapped on navigation
     * @param homeFactory   builds the home screen on first use
     * @param readerFactory builds the reader screen on first use
     */
    public Navigator(StackPane container,
                     Supplier<Parent> homeFactory,
                     Supplier<Parent> readerFactory) {
        this.container = Objects.requireNonNull(container, "container");
        this.homeFactory = Objects.requireNonNull(homeFactory, "homeFactory");
        this.readerFactory = Objects.requireNonNull(readerFactory, "readerFactory");
    }

    /**
     * Shows the given screen, creating it if needed.
     *
     * @param screen which screen to display
     */
    public void show(Screen screen) {
        Parent view = switch (screen) {
            case HOME   -> homeFactory.get();
            case READER -> readerFactory.get();
        };
        container.getChildren().setAll(view);
    }
}
