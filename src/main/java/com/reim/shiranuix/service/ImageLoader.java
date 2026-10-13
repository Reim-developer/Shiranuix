package com.reim.shiranuix.service;

import java.io.IOException;
import java.nio.file.Path;
import java.util.Optional;

/**
 * Loads image files into JavaFX {@code Image}s, off the UI thread.
 *
 * <p>Reading manga pages means decoding multi-megabyte images; doing that on
 * the JavaFX Application Thread would freeze the UI. This interface lets the
 * ViewModel request a page and receive it via a callback once it is ready,
 * without the ViewModel needing to know about threading details.</p>
 *
 * <p>The reader depends only on this interface, so a caching or
 * prefetching implementation can replace the simple one without any UI
 * change.</p>
 */
public interface ImageLoader {

    /**
     * Loads the image at {@code file}, delivering the result on the JavaFX
     * Application Thread.
     *
     * @param file     the image file to load
     * @param callback invoked once the image is loaded, or with
     *                 {@link Optional#empty()} if loading failed
     */
    void load(Path file, ImageCallback callback);

    /**
     * Callback invoked when an image load finishes.
     *
     * <p>Implementations are always invoked on the JavaFX Application Thread,
     * so they may touch the scene graph directly.</p>
     */
    @FunctionalInterface
    interface ImageCallback {

        /**
         * Called when loading completes.
         *
         * @param image the loaded image, or empty on failure
         */
        void onImage(Optional<javafx.scene.image.Image> image);
    }
}
