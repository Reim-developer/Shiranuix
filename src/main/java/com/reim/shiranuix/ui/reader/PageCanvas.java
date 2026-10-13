package com.reim.shiranuix.ui.reader;

import com.reim.shiranuix.model.FitMode;
import javafx.beans.property.ObjectProperty;
import javafx.beans.property.SimpleObjectProperty;
import javafx.geometry.Point2D;
import javafx.geometry.Rectangle2D;
import javafx.scene.image.Image;
import javafx.scene.image.ImageView;
import javafx.scene.layout.Region;
import javafx.scene.shape.Rectangle;

/**
 * The area of the reader that displays a single page.
 *
 * <p>This is the programmatic half of the hybrid UI approach: unlike the
 * toolbars (which are declarative FXML), the page viewport needs precise,
 * dynamic layout — fitting an image to the viewport under a chosen
 * {@link FitMode}, and clamping pan offsets so the image cannot be dragged
 * out of view. That logic is easier to reason about, and to test, in code.</p>
 *
 * <p>The canvas owns an {@link ImageView} and recomputes the fitted viewport
 * whenever the image, the fit mode, or the available size changes.</p>
 */
public final class PageCanvas extends Region {

    private final ImageView imageView = new ImageView();
    private final ObjectProperty<Image> image = new SimpleObjectProperty<>();
    private final ObjectProperty<FitMode> fitMode =
            new SimpleObjectProperty<>(FitMode.FIT_SCREEN);

    /** Size of the source image once decoded, cached for fit calculations. */
    private double sourceWidth;
    private double sourceHeight;

    /** Current pan offset in viewport pixels, relative to the top-left fit. */
    private double panX;
    private double panY;

    /** True while the user is dragging to pan. */
    private boolean panning;

    /** Last mouse position during a pan drag, in local coordinates. */
    private Point2D panAnchor;

    /** Creates an empty canvas wired to react to image/fit changes. */
    public PageCanvas() {
        getStyleClass().add("reader-canvas");

        imageView.setPreserveRatio(true);
        imageView.setSmooth(true);
        // Clip so a panned image never paints outside the viewport.
        Rectangle clip = new Rectangle();
        clip.widthProperty().bind(widthProperty());
        clip.heightProperty().bind(heightProperty());
        imageView.setClip(clip);
        getChildren().add(imageView);

        image.addListener((obs, old, value) -> onImageChanged(value));
        fitMode.addListener((obs, old, value) -> requestLayout());

        // Recompute layout when the viewport is resized.
        widthProperty().addListener((obs, old, value) -> requestLayout());
        heightProperty().addListener((obs, old, value) -> requestLayout());

        installPanHandlers();
    }

    // ------------------------------------------------------------------
    // Properties
    // ------------------------------------------------------------------

    /** @return the image property backing this canvas */
    public ObjectProperty<Image> imageProperty() {
        return image;
    }

    /** @return the fit-mode property backing this canvas */
    public ObjectProperty<FitMode> fitModeProperty() {
        return fitMode;
    }

    // ------------------------------------------------------------------
    // Layout
    // ------------------------------------------------------------------

    /**
     * Positions and scales the image to satisfy the current fit mode.
     *
     * <p>Called by the JavaFX layout pass whenever a bound value or the
     * viewport size changes.</p>
     */
    @Override
    protected void layoutChildren() {
        if (sourceWidth <= 0 || sourceHeight <= 0) {
            // Nothing to draw yet (no image, or it is still decoding).
            imageView.setVisible(false);
            return;
        }
        imageView.setVisible(true);

        double viewW = getWidth();
        double viewH = getHeight();

        double scale = computeScale(viewW, viewH);
        double fitW = sourceWidth * scale;
        double fitH = sourceHeight * scale;

        imageView.setFitWidth(fitW);
        imageView.setFitHeight(fitH);

        // Centre the fitted image, then apply (and clamp) the pan offset.
        double baseX = (viewW - fitW) / 2.0;
        double baseY = (viewH - fitH) / 2.0;

        panX = clamp(panX, Math.min(0, viewW - fitW), Math.max(0, viewW - fitW));
        panY = clamp(panY, Math.min(0, viewH - fitH), Math.max(0, viewH - fitH));

        imageView.setLayoutX(baseX + panX);
        imageView.setLayoutY(baseY + panY);
    }

    /**
     * Computes the scale factor for the current fit mode.
     *
     * @param viewW viewport width
     * @param viewH viewport height
     * @return the scale to apply to the source image
     */
    private double computeScale(double viewW, double viewH) {
        FitMode mode = fitMode.get();
        return switch (mode) {
            // Uniform scale that fits entirely inside the viewport.
            case FIT_SCREEN -> Math.min(viewW / sourceWidth, viewH / sourceHeight);
            // Match viewport width, allowing vertical overflow (scrollable feel).
            case FIT_WIDTH  -> viewW / sourceWidth;
            // Match viewport height, allowing horizontal overflow.
            case FIT_HEIGHT -> viewH / sourceHeight;
            // No scaling: 1 source pixel == 1 screen pixel.
            case ORIGINAL_SIZE -> 1.0;
        };
    }

    // ------------------------------------------------------------------
    // Image handling
    // ------------------------------------------------------------------

    /**
     * Reacts to a new image being set on the canvas.
     *
     * @param value the newly set image, may be {@code null}
     */
    private void onImageChanged(Image value) {
        if (value == null) {
            sourceWidth = 0;
            sourceHeight = 0;
            imageView.setImage(null);
            requestLayout();
            return;
        }

        // If the image is still decoding, width/height are 0 until complete.
        if (value.getWidth() > 0) {
            cacheSize(value);
            imageView.setImage(value);
            resetPan();
            requestLayout();
        } else {
            // Defer until decoding finishes; width becomes non-zero then.
            value.widthProperty().addListener((obs, old, w) -> {
                if (w != null && w.doubleValue() > 0) {
                    cacheSize(value);
                    imageView.setImage(value);
                    resetPan();
                    requestLayout();
                }
            });
        }
    }

    /** Caches the decoded source dimensions used by the fit maths. */
    private void cacheSize(Image value) {
        sourceWidth = value.getWidth();
        sourceHeight = value.getHeight();
    }

    // ------------------------------------------------------------------
    // Pan-by-drag
    // ------------------------------------------------------------------

    /** Wires mouse handlers that let the user drag to pan a zoomed page. */
    private void installPanHandlers() {
        setOnMousePressed(event -> {
            if (isPannable()) {
                panning = true;
                panAnchor = new Point2D(event.getSceneX(), event.getSceneY());
            }
            event.consume();
        });

        setOnMouseDragged(event -> {
            if (panning && panAnchor != null) {
                double dx = event.getSceneX() - panAnchor.getX();
                double dy = event.getSceneY() - panAnchor.getY();
                panX += dx;
                panY += dy;
                panAnchor = new Point2D(event.getSceneX(), event.getSceneY());
                requestLayout();
            }
            event.consume();
        });

        setOnMouseReleased(event -> {
            panning = false;
            panAnchor = null;
            event.consume();
        });
    }

    /**
     * @return {@code true} if the current fit mode leaves room to pan
     */
    private boolean isPannable() {
        if (sourceWidth <= 0) {
            return false;
        }
        double scale = computeScale(getWidth(), getHeight());
        return sourceWidth * scale > getWidth() || sourceHeight * scale > getHeight();
    }

    /** Resets the pan offset to centre the image. */
    private void resetPan() {
        panX = 0;
        panY = 0;
    }

    /** Clamps {@code value} into {@code [min, max]} (min may exceed max). */
    private static double clamp(double value, double min, double max) {
        if (min > max) {
            // Viewport is larger than the image; no panning possible.
            return 0;
        }
        return Math.max(min, Math.min(value, max));
    }

    /**
     * Exposes the current fitted image bounds in viewport coordinates.
     *
     * <p>Exposed for tests: it lets a unit test assert the fit maths without
     * needing a real window.</p>
     *
     * @return the on-screen rectangle occupied by the image, or an empty
     *         rectangle if no image is present
     */
    Rectangle2D getFittedBoundsForTest() {
        if (sourceWidth <= 0 || sourceHeight <= 0) {
            return Rectangle2D.EMPTY;
        }
        double scale = computeScale(getWidth(), getHeight());
        double fitW = sourceWidth * scale;
        double fitH = sourceHeight * scale;
        double baseX = (getWidth() - fitW) / 2.0 + panX;
        double baseY = (getHeight() - fitH) / 2.0 + panY;
        return new Rectangle2D(baseX, baseY, fitW, fitH);
    }

    /**
     * Injects source dimensions without a real {@link Image}.
     *
     * <p>Test seam: lets a unit test exercise the fit-mode arithmetic against
     * known dimensions while staying fully headless.</p>
     *
     * @param width  synthetic source width in pixels
     * @param height synthetic source height in pixels
     */
    void setSourceDimensionsForTest(double width, double height) {
        this.sourceWidth = width;
        this.sourceHeight = height;
    }

    /** @return an immutable snapshot of the current pan offset, for tests */
    Point2D getPanOffsetForTest() {
        return new Point2D(panX, panY);
    }
}
