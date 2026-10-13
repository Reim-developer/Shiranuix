package com.reim.shiranuix.service;

import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Optional;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.atomic.AtomicInteger;
import javafx.application.Platform;
import javafx.scene.image.Image;

/**
 * {@link ImageLoader} that decodes images on a small background pool.
 *
 * <p>Decoding happens on a daemon-thread pool so the UI thread is never
 * blocked. The result is handed back through
 * {@link Platform#runLater(Runnable)} to guarantee callbacks arrive on the
 * JavaFX Application Thread.</p>
 *
 * <p>Instances hold a single-threaded pool by default, which matches the
 * one-page-at-a-time reading mode. When webtoon (continuous scroll) lands,
 * a larger pool can back this class without changing callers.</p>
 */
public final class AsyncImageLoader implements ImageLoader {

    private final ExecutorService executor;

    /**
     * Creates a loader with a background pool whose threads are daemon threads
     * (so they never keep the JVM alive on exit).
     */
    public AsyncImageLoader() {
        AtomicInteger counter = new AtomicInteger();
        this.executor = Executors.newFixedThreadPool(2, runnable -> {
            Thread thread = new Thread(runnable, "image-loader-" + counter.incrementAndGet());
            thread.setDaemon(true);
            return thread;
        });
    }

    @Override
    public void load(Path file, ImageCallback callback) {
        if (file == null || callback == null) {
            return;
        }
        executor.submit(() -> {
            Optional<Image> image = decode(file);
            // Deliver the result on the UI thread.
            Platform.runLater(() -> callback.onImage(image));
        });
    }

    /**
     * Decodes an image file, returning empty if it is missing or unreadable.
     *
     * <p>Runs on a background thread. {@link Image} construction itself is
     * thread-safe; only scene-graph nodes must be touched on the UI thread.</p>
     *
     * @param file the image file
     * @return the decoded image, or empty on failure
     */
    private static Optional<Image> decode(Path file) {
        if (!Files.isRegularFile(file) || !Files.isReadable(file)) {
            return Optional.empty();
        }
        try {
            // backgroundLoading = false: we are already off the UI thread and
            // want the fully decoded image before invoking the callback.
            return Optional.of(new Image(file.toUri().toString(), false));
        } catch (RuntimeException e) {
            // Malformed/unreadable image: treat as a soft failure.
            return Optional.empty();
        }
    }

    /**
     * Shuts down the background pool.
     *
     * <p>Called when the application exits; pending decodes are abandoned.
     * Safe to call more than once.</p>
     */
    public void shutdown() {
        executor.shutdownNow();
    }
}
