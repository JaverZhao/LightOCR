cmake_minimum_required(VERSION 3.20)
project(LightOCR.Native VERSION 1.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_library(LightOCR.Native SHARED
    src/lightocr_api.cpp
    src/ocr_engine.cpp
    src/ocr_config.cpp
    src/ocr_pipeline.cpp
    src/text_detector.cpp
    src/text_recognizer.cpp
    src/image_preprocessor.cpp
    src/box_sorter.cpp
    src/json_serializer.cpp
)

target_include_directories(LightOCR.Native PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

target_compile_definitions(LightOCR.Native PRIVATE
    LIGHTOCR_NATIVE_EXPORTS
    NOMINMAX
)

# ONNX Runtime
set(ONNXRUNTIME_DIR "J:/Javer_Workplace/dev/LightOCR/runtime/onnxruntime/onnxruntime-win-x64-1.21.0"
    CACHE PATH "Path to ONNX Runtime installation")
target_include_directories(LightOCR.Native PRIVATE "${ONNXRUNTIME_DIR}/include")
target_link_directories(LightOCR.Native PRIVATE "${ONNXRUNTIME_DIR}/lib")
target_link_libraries(LightOCR.Native PRIVATE onnxruntime)

# OpenCV (optional for pre/post processing, can use pure C++ fallback)
find_package(OpenCV QUIET)
if(OpenCV_FOUND)
    target_include_directories(LightOCR.Native PRIVATE ${OpenCV_INCLUDE_DIRS})
    target_link_libraries(LightOCR.Native PRIVATE ${OpenCV_LIBS})
    target_compile_definitions(LightOCR.Native PRIVATE USE_OPENCV)
    message(STATUS "OpenCV found: ${OpenCV_VERSION}")
else()
    message(STATUS "OpenCV not found - using pure C++ pre/post processing")
endif()

# Use OpenMP for accelerated image processing if available
find_package(OpenMP QUIET)
if(OpenMP_FOUND)
    target_link_libraries(LightOCR.Native PRIVATE OpenMP::OpenMP_CXX)
endif()
