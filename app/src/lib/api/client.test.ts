import { beforeEach, describe, expect, it, vi } from "vitest"

const requestMock = vi.fn()

vi.mock("axios", () => ({
  default: {
    create: () => ({ request: requestMock }),
  },
}))

describe("apiClient", () => {
  beforeEach(() => {
    requestMock.mockReset()
    vi.resetModules()
  })

  async function loadClient() {
    return await import("./client")
  }

  it("returns the parsed body on a successful GET", async () => {
    requestMock.mockResolvedValue({ status: 200, data: { id: "1", name: "Ana" } })

    const { apiClient } = await loadClient()
    const result = await apiClient.get("/employees/1")

    expect(result).toEqual({ id: "1", name: "Ana" })
    expect(requestMock).toHaveBeenCalledWith({ url: "/employees/1", method: "GET" })
  })

  it("throws an ApiError with the message from the response body on a 4xx response", async () => {
    requestMock.mockResolvedValue({
      status: 404,
      data: { message: "Employee not found." },
    })

    const { apiClient, ApiError } = await loadClient()

    await expect(apiClient.get("/employees/missing")).rejects.toMatchObject({
      message: "Employee not found.",
      status: 404,
    })
    await expect(apiClient.get("/employees/missing")).rejects.toBeInstanceOf(ApiError)
  })

  it("throws an ApiError with a default message when the 4xx response has no JSON body", async () => {
    requestMock.mockResolvedValue({ status: 500, data: null })

    const { apiClient } = await loadClient()

    await expect(apiClient.post("/employees", {})).rejects.toMatchObject({
      message: "Request to /employees failed with status 500.",
      status: 500,
    })
  })

  it("returns undefined for a 204 response instead of parsing the body", async () => {
    requestMock.mockResolvedValue({ status: 204, data: "" })

    const { apiClient } = await loadClient()
    const result = await apiClient.delete("/employees/1")

    expect(result).toBeUndefined()
  })
})
