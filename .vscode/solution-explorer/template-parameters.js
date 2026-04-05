var path = require("path");

module.exports = function (filename, projectPath, folderPath) {
    var namespace = "Unknown";
    var pagePath = path.basename(filename, path.extname(filename)).toLowerCase();
    var pagePathBase = "";
    if (projectPath) {
        namespace = path.basename(projectPath, path.extname(projectPath));

        if (folderPath) {
            namespace += "." + folderPath.replace(path.dirname(projectPath), "").substring(1).replace(/[\\\/]/g, ".");
            var pagePathBase = folderPath.replace(path.dirname(projectPath), "").toLowerCase();
        }
        namespace = namespace.replace(/[\\\-]/g, "_");
    }

    pagePath = path.join(pagePathBase, pagePath);
    if (pagePath.substring(0, 6) == "/pages") {
        pagePath = pagePath.substring(6);
    }

    return {
        namespace: namespace,
        name: path.basename(filename, path.extname(filename)),
        pagePath: pagePath
    }
};
