package com.reim.shiranuix.ui.reader;

import com.reim.shiranuix.model.FitMode;
import com.reim.shiranuix.model.ReadingDirection;
import java.util.Objects;
import javafx.beans.binding.Bindings;
import javafx.fxml.FXML;
import javafx.scene.control.Button;
import javafx.scene.control.ComboBox;
import javafx.scene.control.Label;
import javafx.scene.control.Slider;
import javafx.scene.layout.BorderPane;
import javafx.scene.layout.StackPane;
import javafx.stage.Stage;

/**
 * Code-behind for {@code reader-view.fxml}.
 *
 * <p>Thin by design: it builds the {@link PageCanvas} into the viewport,
 * binds the toolbar controls to the {@link ReaderViewModel}, and translates
 * user gestures into view-model commands. No reading logic lives here.</p>
 *
 * <p>Like {@code HomeController}, the view model is injected after load via
 * {@link #setViewModel(ReaderViewModel)}.</p>
 */
public final class ReaderController {

    @FXML private BorderPane root;
    @FXML private StackPane viewport;
    @FXML private Button backButton;
    @FXML private Button previousButton;
    @FXML private Button nextButton;
    @FXML private Button fullscreenButton;
    @FXML private Label titleLabel;
    @FXML private Label pageLabel;
    @FXML private Slider pageSlider;
    @FXML private ComboBox<ReadingDirection> directionComboBox;
    @FXML private ComboBox<FitMode> fitModeComboBox;

    private ReaderViewModel viewModel;
    private PageCanvas canvas;

    /**
     * Injects the view model and binds it to the view.
     *
     * @param viewModel the view model backing this screen
     */
    public void setViewModel(ReaderViewModel viewModel) {
        this.viewModel = Objects.requireNonNull(viewModel, "viewModel");

        buildCanvas();
        configureControls();
        bindState();
        wireActions();
    }

    /**
     * Creates the page canvas and installs it in the viewport.
     *
     * <p>The canvas is programmatic (not FXML) because its layout maths —
     * fitting and panning — is clearer in code.</p>
     */
    private void buildCanvas() {
        canvas = new PageCanvas();
        // Stretch the canvas to fill whatever space the viewport has.
        StackPane.setAlignment(canvas, javafx.geometry.Pos.CENTER);
        viewport.getChildren().setAll(canvas);
    }

    /** Populates the combo boxes with the presentation axes. */
    private void configureControls() {
        directionComboBox.getItems().setAll(ReadingDirection.values());
        fitModeComboBox.getItems().setAll(FitMode.values());
    }

    /** Binds view-model state to the controls. */
    private void bindState() {
        // Title reflects the open chapter.
        titleLabel.textProperty().bind(Bindings.createStringBinding(
                () -> viewModel.chapterProperty().get() == null
                        ? ""
                        : viewModel.chapterProperty().get().getTitle(),
                viewModel.chapterProperty()));

        // Page label reflects position, e.g. "3 / 42".
        pageLabel.textProperty().bind(Bindings.createStringBinding(
                () -> viewModel.pageCountProperty().get() == 0
                        ? "– / –"
                        : viewModel.currentPageIndexProperty().get()
                                + " / " + viewModel.pageCountProperty().get(),
                viewModel.currentPageIndexProperty(), viewModel.pageCountProperty()));

        // Enable/disable navigation buttons from the derived booleans.
        previousButton.disableProperty().bind(viewModel.canGoPreviousProperty().not());
        nextButton.disableProperty().bind(viewModel.canGoNextProperty().not());

        // Slider tracks the current page (1-based, matching the model).
        pageSlider.minProperty().set(1);
        pageSlider.maxProperty().bind(viewModel.pageCountProperty());
        pageSlider.valueProperty().addListener((obs, old, value) -> {
            // Guard against feedback loops while the slider drags.
            if (value != null && value.intValue() != viewModel.currentPageIndexProperty().get()) {
                viewModel.goToPage(value.intValue());
            }
        });
        viewModel.currentPageIndexProperty().addListener((obs, old, value) -> {
            int current = (int) pageSlider.getValue();
            if (value != null && value.intValue() != current) {
                pageSlider.setValue(value.intValue());
            }
        });

        // Keep the combo boxes in sync with the view model.
        directionComboBox.valueProperty().bindBidirectional(viewModel.readingDirectionProperty());
        fitModeComboBox.valueProperty().bindBidirectional(viewModel.fitModeProperty());

        // The canvas shows the current image under the current fit mode.
        canvas.imageProperty().bind(viewModel.currentImageProperty());
        canvas.fitModeProperty().bind(viewModel.fitModeProperty());
    }

    /** Wires button clicks and keyboard-ish actions to view-model commands. */
    private void wireActions() {
        backButton.setOnAction(event -> viewModel.goBack());
        previousButton.setOnAction(event -> viewModel.goPrevious());
        nextButton.setOnAction(event -> viewModel.goNext());

        // Fullscreen toggles the owning stage.
        fullscreenButton.setOnAction(event -> toggleFullscreen());
    }

    /**
     * Toggles fullscreen on the window hosting this view.
     */
    private void toggleFullscreen() {
        if (root.getScene() != null && root.getScene().getWindow() instanceof Stage stage) {
            stage.setFullScreen(!stage.isFullScreen());
        }
    }
}
