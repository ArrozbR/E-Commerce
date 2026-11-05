import { copyFileSync, mkdirSync } from "node:fs";

const target = "wwwroot/lib/gsap";
mkdirSync(target, { recursive: true });

for (const file of ["gsap.min.js", "ScrollTrigger.min.js"]) {
    copyFileSync(`node_modules/gsap/dist/${file}`, `${target}/${file}`);
}
