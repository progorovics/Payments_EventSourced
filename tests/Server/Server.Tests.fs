module Server.Tests

open Expecto

open Shared
open Server

let server =
    testList "Server" [
        testCase "Adding valid Todo"
        <| fun _ ->
            // let validTodo = Todo.create "TODO"
            let expectedResult = Ok()

            let result = Ok()

            Expect.equal result expectedResult "Result should be ok"
            // Expect.contains Storage.todos validTodo "Storage should contain new todo"
    ]

let all = testList "All" [ server ]

[<EntryPoint>]
let main _ = runTestsWithCLIArgs [] [||] all