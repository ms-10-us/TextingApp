import { describe, expect, it } from "vitest";
import { TestDriver } from "testdriverai/vitest/hooks";

describe("Smoke Test", () => {
  it("should load the application and verify content", async (context) => {
    const testdriver = TestDriver(context);

    await testdriver.provision.chrome({ url: "https://example.com" });

    const assertResult = await testdriver.assert(
      "The page displays the text 'This domain is for use in documentation examples'"
    );
    expect(assertResult).toBeTruthy();
  });

  it("should navigate via learn more link", async (context) => {
    const testdriver = TestDriver(context);

    await testdriver.provision.chrome({ url: "https://example.com" });

    await testdriver.scroll("down", { amount: 300 });

    const learnMore = await testdriver.find("Learn more link");
    await learnMore.click();

    const assertResult = await testdriver.assert(
      "The IANA page displays 'Example Domains' and navigation links"
    );
    expect(assertResult).toBeTruthy();
  });
});
