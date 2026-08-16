import { cp, mkdir, rm } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const webDirectory = path.resolve(scriptDirectory, "..");
const sourceDirectory = path.join(webDirectory, "out");
const destinationDirectory = path.resolve(
  webDirectory,
  "..",
  "src",
  "LexTime.Api",
  "wwwroot",
);

await rm(destinationDirectory, { force: true, recursive: true });
await mkdir(destinationDirectory, { recursive: true });
await cp(sourceDirectory, destinationDirectory, { recursive: true });

console.log(`Synced static dashboard to ${destinationDirectory}`);
