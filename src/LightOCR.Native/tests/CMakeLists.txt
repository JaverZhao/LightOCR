cmake_minimum_required(VERSION 3.14)
project(test_paddle_load CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded")

set(PADDLE_LIB "J:/Javer_Workplace/dev/LightOCR/runtime/paddle" CACHE PATH "Paddle Inference root")

include_directories("${PADDLE_LIB}/paddle/include")
link_directories("${PADDLE_LIB}/paddle/lib")

add_executable(test_paddle_load test_paddle_load.cpp)
target_link_libraries(test_paddle_load paddle_inference)
