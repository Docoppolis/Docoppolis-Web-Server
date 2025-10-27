document.getElementById("redirectBtn").addEventListener("click", async () => {
    try {
        const res = await fetch("/demo/redirect", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ example: "test" })
        });

        if (!res.ok) throw new Error(`HTTP ${res.status}`);

        const redirectPath = await res.text();
        console.log("Redirecting to:", redirectPath);
        window.location.href = redirectPath; // Go to returned page
    } catch (err) {
        alert("Request failed: " + err.message);
    }
});
