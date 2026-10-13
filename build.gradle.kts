/*
 * Shiranuix – a manga reader desktop application.
 *
 * Build script for the JavaFX application. Uses the OpenJFX Gradle plugin
 * to manage JavaFX dependencies and the `application` plugin to define
 * the launch entry point.
 */

plugins {
    application
    alias(libs.plugins.javafx)
}

group = "com.reim.shiranuix"
version = "0.1.0"

java {
    toolchain {
        // JavaFX 24 requires JDK 21+; the toolchain pins a reproducible JDK.
        languageVersion = JavaLanguageVersion.of(25)
    }
}

repositories {
    mavenCentral()
}

javafx {
    version = libs.versions.javafx.get()
    modules("javafx.controls", "javafx.fxml")
}

dependencies {
    // --- Testing: JUnit 5 (unit) + TestFX (JavaFX UI) --------------------
    testImplementation(platform(libs.junit.bom))
    testImplementation(libs.junit.jupiter)
    testRuntimeOnly(libs.junit.platform.launcher)

    testImplementation(libs.testfx.core)
    testImplementation(libs.testfx.junit5)
    // Hamcrest matchers, required by TestFX's NodeQuery API.
    testImplementation(libs.hamcrest)
    // Monocle provides a headless Glass platform so UI tests can run
    // without a display (activated with -Pheadless, see `test` task below).
    testRuntimeOnly(libs.monocle)
}

application {
    mainClass = "com.reim.shiranuix.Main"
    // At runtime JavaFX lives on the module path (javafx.graphics module),
    // so enable native access for that module to avoid the JDK 24+ warning.
    applicationDefaultJvmArgs = listOf("--enable-native-access=javafx.graphics")
}

tasks.test {
    useJUnitPlatform()
    testLogging {
        events("passed", "skipped", "failed")
    }

    // Always enable native access (JavaFX loads native libraries in the
    // test JVM too). On top of that, when -Pheadless is requested, swap
    // the Glass platform for Monocle so UI tests run without a display:
    //   ./gradlew test -Pheadless
    val testJvmArgs = mutableListOf("--enable-native-access=ALL-UNNAMED")
    if (project.hasProperty("headless")) {
        testJvmArgs += listOf(
            "-Dglass.platform=Monocle",
            "-Dmonocle.platform=Headless",
            "-Dprism.order=sw",
            "-Djava.awt.headless=true"
        )
    }
    jvmArgs(testJvmArgs)
}

tasks.withType<JavaCompile>().configureEach {
    options.encoding = "UTF-8"
    options.compilerArgs.add("-Xlint:all")
}

tasks.javadoc {
    (options as StandardJavadocDocletOptions).encoding = "UTF-8"
}
