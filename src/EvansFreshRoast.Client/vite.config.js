import { defineConfig } from 'vite'

export default defineConfig({
    root: "fable",
    build: {
        outDir: "../../EvansFreshRoast.Api/wwwroot",
        emptyOutDir: true,
        sourcemap: true
    }
});