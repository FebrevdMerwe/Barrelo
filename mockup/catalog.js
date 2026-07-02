/* Static stand-in for GET /api/games — what a real GameCatalog.ListAvailable() would return. */
(function (global) {
  "use strict";
  global.GAME_CATALOG = [
    {
      id: "x01",
      name: "501",
      tagline: "Race to zero. Finish on a double — or the bull.",
      meta: "2 players · double out · best of 5 legs"
    },
    {
      id: "cricket",
      name: "Cricket",
      tagline: "Close 20 down to 15 and bull. Outscore the table before they close you out.",
      meta: "2 players · 20–15 & bull · best of 3 legs"
    },
    {
      id: "kickoff",
      name: "Kickoff",
      tagline: "One ball, one pitch. Miss the frame and it's their throw-in.",
      meta: "2 players · one shared ball · best of 3 legs"
    },
    {
      id: "puttputt",
      name: "Putt-Putt",
      tagline: "Nine holes, lowest total putts wins. Aim your putt off the dartboard and bounce it home.",
      meta: "2 players · 9 holes · fewest putts wins"
    }
  ];
})(window);
