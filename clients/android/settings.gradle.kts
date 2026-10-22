pluginManagement {
    repositories {
        google()
        maven("https://repo1.maven.org/maven2")
        gradlePluginPortal()
    }
}

dependencyResolutionManagement {
    repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS)
    repositories {
        google()
        maven("https://repo1.maven.org/maven2")
    }
}

rootProject.name = "9mRoomAndroid"
include(":app")
