/// <binding BeforeBuild='clean, min' Clean='clean' />
"use strict";

var gulp = require("gulp"),
    rm = require("gulp-rimraf"),
    plumber = require("gulp-plumber"),
    concat = require("gulp-concat"),
    cleanCss= require("gulp-clean-css"),
    uglify = require("gulp-uglify");

var paths = {
    webroot: "./wwwroot/"
};

paths.js = paths.webroot + "js/**/*.js";
paths.minJs = paths.webroot + "js/**/*.min.js";
paths.css = paths.webroot + "css/**/*.css";
paths.minCss = paths.webroot + "css/**/*.min.css";
paths.concatJsDest = paths.webroot + "js/site.min.js";
paths.concatCssDest = paths.webroot + "css/site.min.css";



gulp.task("clean:js", function () {
    return gulp.src(paths.concatJsDest, { allowEmpty: true })
        .pipe(rm());
});

gulp.task("clean:css", function () {
    return gulp.src(paths.concatCssDest, { allowEmpty: true })
        .pipe(rm());
});

gulp.task("clean", gulp.series("clean:js", "clean:css"));

gulp.task("min:js", function () {
    return gulp.src([paths.js, "!" + paths.minJs], { base: "." })
        .pipe(plumber({
            errorHandler: function (error) {
                console.log(error.message);
                generator.emit("end");
            }
        }))
        .pipe(concat(paths.concatJsDest))
        .pipe(uglify())
        .pipe(gulp.dest("."));
});

gulp.task("min:css", function () {
    return gulp.src([paths.css, "!" + paths.minCss])
        .pipe(plumber({
            errorHandler: function (error) {
                console.log(error.message);
                generator.emit("end");
            }
        }))
        .pipe(concat(paths.concatCssDest))
        .pipe(cleanCss())
        .pipe(gulp.dest("."));
});


gulp.task("min", gulp.series("min:js", "min:css"));
