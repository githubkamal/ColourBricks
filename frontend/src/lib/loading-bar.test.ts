import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  isLoadingActive,
  startApiCall,
  startNavigation,
  stopApiCall,
  stopNavigation,
  subscribeLoading,
} from "./loading-bar";

describe("loading-bar", () => {
  beforeEach(() => {
    // Drain any counts left over from a previous test.
    while (isLoadingActive()) {
      stopApiCall();
      stopNavigation();
    }
  });

  it("LoadingBar_ApiCallStartAndStop_TogglesActive", () => {
    expect(isLoadingActive()).toBe(false);
    startApiCall();
    expect(isLoadingActive()).toBe(true);
    stopApiCall();
    expect(isLoadingActive()).toBe(false);
  });

  it("LoadingBar_OverlappingApiCalls_StaysActiveUntilTheLastOneStops", () => {
    startApiCall();
    startApiCall();
    stopApiCall();
    expect(isLoadingActive()).toBe(true);
    stopApiCall();
    expect(isLoadingActive()).toBe(false);
  });

  it("LoadingBar_StopApiCall_NeverGoesNegative", () => {
    stopApiCall();
    stopApiCall();
    startApiCall();
    expect(isLoadingActive()).toBe(true);
    stopApiCall();
    expect(isLoadingActive()).toBe(false);
  });

  it("LoadingBar_Navigation_TogglesActiveIndependentlyOfApiCalls", () => {
    startNavigation();
    expect(isLoadingActive()).toBe(true);
    startApiCall();
    stopNavigation();
    expect(isLoadingActive()).toBe(true); // the API call is still in flight
    stopApiCall();
    expect(isLoadingActive()).toBe(false);
  });

  it("LoadingBar_Subscribers_NotifiedOnlyOnActiveTransitions", () => {
    const listener = vi.fn();
    const unsubscribe = subscribeLoading(listener);

    startApiCall(); // 0 -> 1: notifies
    startApiCall(); // 1 -> 2: no transition, no notify
    stopApiCall(); // 2 -> 1: no transition, no notify
    stopApiCall(); // 1 -> 0: notifies

    expect(listener).toHaveBeenCalledTimes(2);
    unsubscribe();
  });
});
