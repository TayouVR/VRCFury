import process from 'node:process';
import fs from 'node:fs/promises';
import fsPlain from 'node:fs';
import {create as tarCreate} from 'tar';
import { hashFile } from 'hasha';
import semver from 'semver';
import tmp from 'tmp-promise';
import { spawn } from 'promisify-child-process';
import archiver from 'archiver';

const versionJson = await readJson('../versions/updates.json');
const vccJson = await readJson('../versions/vcc.json');
await rmdir('dist');
await fs.mkdir('dist');

const allTags = await getTags();

for (const dir of await fs.readdir('.')) {
    const packageJsonPath = `${dir}/package.json`
    if (!await checkFileExists(packageJsonPath)) {
        continue;
    }

    console.log(`Packaging ${dir}`);

    const json = await readJson(packageJsonPath);
    const name = json.name;

    let existing = versionJson.packages.find(e => e.id === name);
    if (existing) {
        json.version = existing.latestVersion;
        await writeJson(packageJsonPath, json);
        if ((await md5Dir(dir)) === existing.hash) {
            console.log("Hash already matches, skipping ...");
            continue;
        }
    }

    const tagPrefix = `${name}/`;
    let version = await getNextVersion(allTags, tagPrefix);
    const tagName = `${tagPrefix}${version}`
    json.version = version;
    await writeJson(packageJsonPath, json);

    const outputFilename = `${name}-${version}.tgz`;
    const outputPath = `dist/${outputFilename}`;
    const outputUrl = `https://github.com/VRCFury/VRCFury/releases/download/${encodeURIComponent(tagName)}/${encodeURIComponent(outputFilename)}`;
    await createTar(dir, outputPath);

    if (!existing) {
        existing = { id: name };
        versionJson.packages.push(existing);
    }
    existing.latestVersion = version;
    existing.hash = await hashFile(outputPath, {algorithm: 'sha256'});
    existing.displayName = json.displayName;
    existing.latestUpmTargz = outputUrl;

    if (name === "com.vrcfury.installer") {
        const updater = versionJson.packages.find(e => e.id === "com.vrcfury.updater");
        if (updater) {
            updater.latestVersion = existing.latestVersion;
            updater.hash = existing.hash;
            updater.displayName = existing.displayName;
            updater.latestUpmTargz = existing.latestUpmTargz;
        }
    }
    console.log(`Adding to version repository with version ${version}`);

    const outputZipFilename = `${name}-${version}-vcc.zip`;
    const outputZipPath = `dist/${outputZipFilename}`;
    const outputZipUrl = `https://github.com/VRCFury/VRCFury/releases/download/${encodeURIComponent(tagName)}/${encodeURIComponent(outputZipFilename)}`;
    await createZip(dir, outputZipPath);

    if (name === 'com.vrcfury.vrcfury') {
        let existingVcc = vccJson.packages[name];
        if (!existingVcc) {
            existingVcc = vccJson.packages[name] = {
                versions: {}
            };
        }
        if (existingVcc.versions[version]) {
            throw new Error("Version already exists in vcc.json");
        }
        const vccPackage = JSON.parse(JSON.stringify(json));
        vccPackage.url = outputZipUrl;
        existingVcc.versions[version] = vccPackage;
    }

    await spawn('git', [ 'config', '--global', 'user.email', 'noreply@vrcfury.com' ], { stdio: "inherit" });
    await spawn('git', [ 'config', '--global', 'user.name', 'VRCFury Releases' ], { stdio: "inherit" });
    await spawn('git', [ 'commit', '-m', `${json.displayName} v${version}`, packageJsonPath ], { stdio: "inherit" });
    await spawn('git', [ 'tag', tagName ], { stdio: "inherit" });
    await spawn('git', [ 'push', 'origin', tagName ], { stdio: "inherit" });
    await spawn('git', [ 'checkout', process.env.GITHUB_SHA ], { stdio: "inherit" });

    await spawn('gh', [
        'release',
        'create',
        tagName,
        outputPath,
        outputZipPath,
        '--title', `${json.displayName} v${version}`,
        '--verify-tag'
    ], { stdio: "inherit" });
}

await writeJson('../versions/updates.json', versionJson);

for (const p of Object.values(vccJson.packages)) {
    p.versions = Object.fromEntries(
        Object.entries(p.versions)
            .sort(([v1], [v2]) => semver.compare(v2, v1)),
    );
}
await writeJson('../versions/vcc.json', vccJson);

function checkFileExists(file) {
    return fs.access(file, fs.constants.F_OK)
        .then(() => true)
        .catch(() => false)
}

async function md5Dir(dir) {
    const tmpFile = (await tmp.file()).path;
    await createTar(dir, tmpFile);
    const md5 = await hashFile(tmpFile, {algorithm: 'sha256'});
    await fs.unlink(tmpFile);
    return md5;
}
async function readJson(file) {
    return JSON.parse(await fs.readFile(file, {encoding: 'utf-8'}));
}
async function writeJson(file, obj) {
    await fs.writeFile(file, JSON.stringify(obj, null, 2));
}
async function rmdir(path) {
    if (await checkFileExists(path)) {
        await fs.rm(path, {recursive: true});
    }
}
async function createTar(dir, outputFilename) {
    await tarCreate({
        gzip: true,
        cwd: dir,
        file: outputFilename,
        portable: true,
        noMtime: true,
        prefix: 'package/'
    }, await fs.readdir(dir));
}
async function createZip(dir, outputFilename) {
    const output = fsPlain.createWriteStream(outputFilename);
    const archive = archiver('zip', {
        zlib: { level: 9 } // Sets the compression level.
    });
    archive.pipe(output);
    archive.directory(dir, false);
    await archive.finalize();
}

async function getTags() {
    const { stdout, stderr } = await spawn('git', ['ls-remote', '--tags', 'origin'], {encoding: 'utf8'});
    return (stdout+'')
        .split('\n')
        .filter(line => line.includes("refs/tags/"))
        .map(line => line.substring(line.indexOf('refs/tags/') + 10).trim())
        .filter(line => line !== "");
}
async function getNextVersion(allTags, prefix) {
    const existingVersions = allTags
        .filter(tag => tag.startsWith(prefix))
        .map(tag => tag.substring(prefix.length));
    if (existingVersions.length === 0) {
        existingVersions.push('1.0.0');
    }
    if (process.env.GITHUB_REF_NAME === "main") {
        const maxVersion = semver.maxSatisfying(existingVersions, '*');
        return semver.inc(maxVersion, 'minor');
    }
    const maxVersion = semver.maxSatisfying(existingVersions, '*', { includePrerelease: true });
    return semver.inc(maxVersion, 'prerelease', 'beta', 1);
}
