package com.reim.shiranuix.service;

import com.reim.shiranuix.model.Chapter;
import java.nio.file.Path;
import java.util.Optional;

/**
 * Reads a folder of images into a {@link Chapter}.
 *
 * <p>Implementations decide what counts as an image and in what order pages
 * appear. The reader only depends on this interface, so a future
 * implementation (e.g. one that reads a ZIP/CBZ archive, or a remote
 * source) can be swapped in without touching the UI.</p>
 */
public interface FolderScanner {

    /**
     * Scans the given folder and builds a chapter from its images.
     *
     * @param folder the folder to scan
     * @return the chapter, or empty if the folder contains no usable images
     *         or is not a readable directory
     */
    Optional<Chapter> scan(Path folder);
}
